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
        private static readonly ResourceTypeFactory TypeFactory = new ResourceTypeFactory();

        public static IResourceTypeRegistrar Instance { get; } = new ResourceTypeRegistrar();

        private ResourceTypeRegistrar()
        {
        }

        private ResourceType? TryLookupResource(ResourceTypeReference typeReference)
        {
            var resourceType = TypeLoader.TryLoadResource(typeReference.FullyQualifiedType, typeReference.ApiVersion);
            if (resourceType == null)
            {
                return null;
            }

            return TypeFactory.GetResourceType(resourceType);
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