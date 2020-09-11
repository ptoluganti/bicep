// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Linq;
using System.Collections.Generic;
using Bicep.Core.Resources;
using Bicep.Types.Arm;

namespace Bicep.Core.TypeSystem
{
    public class ResourceTypeRegistrar : IResourceTypeRegistrar
    {
        private static readonly IDictionary<string, ResourceTypeFactory?> TypeFactories = new Dictionary<string, ResourceTypeFactory?>(StringComparer.OrdinalIgnoreCase);

        private static ResourceTypeFactory? GetTypeFactory(ResourceTypeReference typeReference)
        {
            var key = $"{typeReference.Namespace}@{typeReference.ApiVersion}";

            if (!TypeFactories.TryGetValue(key, out var typeFactory))
            {
                var types = TypeLoader.LoadTypes(typeReference.Namespace, typeReference.ApiVersion);
                typeFactory = types.Any() ? new ResourceTypeFactory(types, typeReference.ApiVersion) : null;
                TypeFactories[key] = typeFactory;
            }

            return typeFactory;
        }

        public static IResourceTypeRegistrar Instance { get; } = new ResourceTypeRegistrar();

        private ResourceTypeRegistrar()
        {
        }

        private ResourceType? TryLookupResource(ResourceTypeReference typeReference)
        {
            var typeFactory = GetTypeFactory(typeReference);
            if (typeFactory == null)
            {
                return null;
            }

            return typeFactory.TryGetResourceType(typeReference);
        }

        public ResourceType LookupType(ResourceTypeReference typeReference)
        {
            var resourceType = TryLookupResource(typeReference);

            if (resourceType != null)
            {
                return resourceType;
            }

            // TODO move default definition into types assembly
            return new ResourceType(typeReference.FullyQualifiedType, LanguageConstants.CreateResourceProperties(typeReference), additionalProperties: null, typeReference);            
        }

        public bool HasTypeDefined(ResourceTypeReference typeReference)
            => TryLookupResource(typeReference) != null;
    }
}