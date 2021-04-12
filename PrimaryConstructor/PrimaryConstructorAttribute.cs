using System;

namespace PrimaryConstructor
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class PrimaryConstructorAttribute : Attribute
    {
    }
}