﻿using System;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
// ReSharper disable once CheckNamespace
public class IgnorePrimaryConstructorAttribute : Attribute
{
}
