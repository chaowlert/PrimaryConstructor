using System;

namespace PrimaryConstructor
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class IgnorePrimaryConstructorAttribute : Attribute
    {
    }
}