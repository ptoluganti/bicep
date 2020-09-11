// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Linq;
using System.Collections.Generic;
using Bicep.Core.Resources;

namespace Bicep.Core.TypeSystem
{
    public class ResourceTypeFactory
    {
        private readonly Dictionary<Types.Concrete.TypeBase, TypeSymbol> typeCache;
        private readonly IReadOnlyDictionary<string, Types.Concrete.ResourceType> resourceTypes;
        private readonly string apiVersion;

        public ResourceTypeFactory(IEnumerable<Types.Concrete.TypeBase> serializedTypes, string apiVersion)
        {
            typeCache = new Dictionary<Types.Concrete.TypeBase, TypeSymbol>();
            resourceTypes = serializedTypes.OfType<Types.Concrete.ResourceType>().ToDictionary(x => x.Name ?? throw new ArgumentException(), StringComparer.OrdinalIgnoreCase);
            this.apiVersion = apiVersion;
        }

        public ResourceType? TryGetResourceType(ResourceTypeReference resourceTypeReference)
        {
            if (!resourceTypes.TryGetValue(resourceTypeReference.FullyQualifiedType, out var resourceType))
            {
                return null;
            }

            return GetTypeSymbol(resourceType) as ResourceType;
        }

        private TypeSymbol GetTypeSymbol(Types.Concrete.TypeBase serializedType)
        {
            if (!typeCache.TryGetValue(serializedType, out var typeSymbol))
            {
                typeSymbol = ToTypeSymbol(serializedType);
                typeCache[serializedType] = typeSymbol;
            }

            return typeSymbol;
        }

        private ITypeReference GetTypeReference(Types.Concrete.ITypeReference input)
            => new DeferredTypeReference(() => GetTypeSymbol(input.Type));

        private TypeProperty GetTypeProperty(string name, Types.Concrete.ObjectProperty input)
        {
            var type = input.Type ?? throw new ArgumentException();

            return new TypeProperty(name, GetTypeReference(type), GetTypePropertyFlags(input));
        }

        private static TypePropertyFlags GetTypePropertyFlags(Types.Concrete.ObjectProperty input)
        {
            var flags = TypePropertyFlags.None;

            if (input.Flags.HasFlag(Types.Concrete.ObjectPropertyFlags.Required))
            {
                flags |= TypePropertyFlags.Required;
            }
            if (input.Flags.HasFlag(Types.Concrete.ObjectPropertyFlags.ReadOnly))
            {
                flags |= TypePropertyFlags.ReadOnly;
            }
            if (input.Flags.HasFlag(Types.Concrete.ObjectPropertyFlags.WriteOnly))
            {
                flags |= TypePropertyFlags.WriteOnly;
            }
            if (input.Flags.HasFlag(Types.Concrete.ObjectPropertyFlags.DeployTimeConstant))
            {
                flags |= TypePropertyFlags.SkipInlining;
            }

            return flags;
        }

        private TypeSymbol ToTypeSymbol(Types.Concrete.TypeBase typeBase)
        {
            switch (typeBase)
            {
                case Types.Concrete.BuiltInType builtInType:
                    return builtInType.Kind switch {
                        Types.Concrete.BuiltInTypeKind.Any => LanguageConstants.Any,
                        Types.Concrete.BuiltInTypeKind.Null => LanguageConstants.Null,
                        Types.Concrete.BuiltInTypeKind.Bool => LanguageConstants.Bool,
                        Types.Concrete.BuiltInTypeKind.Int => LanguageConstants.Int,
                        Types.Concrete.BuiltInTypeKind.String => LanguageConstants.String,
                        Types.Concrete.BuiltInTypeKind.Object => LanguageConstants.Object,
                        Types.Concrete.BuiltInTypeKind.Array => LanguageConstants.Array,
                        Types.Concrete.BuiltInTypeKind.ResourceRef => LanguageConstants.ResourceRef,
                        _ => throw new ArgumentException(),
                    };
                case Types.Concrete.ObjectType objectType:
                {
                    var name = objectType.Name ?? string.Empty; // TODO
                    var properties = objectType.Properties ?? new Dictionary<string, Types.Concrete.ObjectProperty>();
                    var additionalProperties = objectType.AdditionalProperties != null ? GetTypeReference(objectType.AdditionalProperties) : null;

                    return new NamedObjectType(name, properties.Select(kvp => GetTypeProperty(kvp.Key, kvp.Value)), additionalProperties, TypePropertyFlags.None);
                }
                case Types.Concrete.ArrayType arrayType:
                {
                    var itemType = arrayType.ItemType ?? throw new ArgumentException();

                    return new TypedArrayType(GetTypeReference(itemType));
                }
                case Types.Concrete.ResourceType resourceType:
                {
                    var name = resourceType.Name ?? throw new ArgumentException();
                    var body = (resourceType.Body?.Type as Types.Concrete.ObjectType) ?? throw new ArgumentException();
                    
                    var properties = body.Properties ?? throw new ArgumentException();
                    var additionalProperties = body.AdditionalProperties != null ? GetTypeReference(body.AdditionalProperties) : null;
                    var resourceTypeReference = ResourceTypeReference.TryParse($"{name}@{apiVersion}") ?? throw new ArgumentException();

                    return new ResourceType(name, properties.Select(kvp => GetTypeProperty(kvp.Key, kvp.Value)), additionalProperties, resourceTypeReference);
                }
                case Types.Concrete.UnionType unionType:
                {
                    var elements = unionType.Elements ?? throw new ArgumentException();
                    return UnionType.Create(elements.Select(GetTypeReference));
                }
                case Types.Concrete.StringLiteralType stringLiteralType:
                    var value = stringLiteralType.Value ?? throw new ArgumentException();
                    return new StringLiteralType(value);
                case Types.Concrete.DiscriminatedObjectType discriminatedObjectType:
                {
                    var name = discriminatedObjectType.Name ?? throw new ArgumentException();
                    var discriminator = discriminatedObjectType.Discriminator ?? throw new ArgumentException();
                    var elements = discriminatedObjectType.Elements ?? throw new ArgumentException();
                    var baseProperties = discriminatedObjectType.BaseProperties ?? throw new ArgumentException();

                    var elementReferences = elements.Select(kvp => new DeferredTypeReference(() => ToCombinedType(discriminatedObjectType.BaseProperties, kvp.Key, kvp.Value)));

                    return new DiscriminatedObjectType(name, discriminator, elementReferences);
                }
                default:
                    throw new ArgumentException();
            }
        }

        private NamedObjectType ToCombinedType(IEnumerable<KeyValuePair<string, Types.Concrete.ObjectProperty>> baseProperties, string name, Types.Concrete.ITypeReference extendedType)
        {
            if (!(extendedType.Type is Types.Concrete.ObjectType objectType))
            {
                throw new ArgumentException();
            }

            var properties = objectType.Properties ?? throw new ArgumentException();
            var additionalProperties = objectType.AdditionalProperties != null ? GetTypeReference(objectType.AdditionalProperties) : null;

            var extendedProperties = baseProperties.Concat(properties);

            return new NamedObjectType(name, extendedProperties.Select(kvp => GetTypeProperty(kvp.Key, kvp.Value)), additionalProperties, TypePropertyFlags.None);
        }
    }
}