// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using Bicep.Core.Resources;

namespace Bicep.Core.TypeSystem
{
    public interface IResourceTypeRegistrar
    {
        ResourceType LookupType(ResourceTypeReference reference);

        bool HasTypeDefined(ResourceTypeReference typeReference);
    }
}