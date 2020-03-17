---
title: Value Comparers - EF Core
author: ajcvickers
ms.date: 03/17/2020
uid: core/modeling/value-comparers
---

# Value Comparers

> [!NOTE]  
> This feature is new in EF Core 3.0.

## Background

EF Core needs to compare property values when:

* Determining whether or not a property has been changed as part of [detecting changes for updates](../saving/basic)
* Determining whether or not two key values are the same when resolving relationships 

This is handled automatically for common primitive types with little internal structure, such as int, DateTime, etc.

For more complex types, choices need to be made as to how to do the comparison.
For example, a byte array could be compared:

* By reference, such that a difference is only detected if a new byte array is used
* By deep comparison, such that mutation of the bytes in the array is detected

EF Core uses the first of these approaches for non-key byte arrays.
That is, only references are compared and a change is detected when an existing byte array is replaced with a new one.
This is a pragmatic decision that avoids deep comparison of many large byte arrays when executing SaveChanges.
But the common scenario of replacing, say, an image with a different image is handled in a performant way.

On the other hand, reference equality would not work when byte arrays are used to represent binary keys.
It's very unlikely that an FK property is set to the _same instance_ as a PK property to which it needs to be compared.
Therefore, EF Core uses deep comparisons for byte arrays acting as keys.
This is unlikely to have a big performance hit since binary keys are usually short.

### Snapshots

Deep comparisons on mutable types means that EF Core needs the ability to create a deep "snapshot" of the property value.
Just copying the reference instead would result in mutating both the current value and the snapshot, since they are _the same object_.
Therefore, when deep comparisons are used, deep snapshotting is also required.

## Properties with value converters

In the case above, EF Core has native mapping support for byte arrays and so can automatically choose appropriate defaults.
However, if the property is mapped through a [value converter](value-conversions), then EF Core can't always determine the appropriate comparison to use.
Instead, EF Core always uses the default equality comparison defined by the property type.
This is often correct, but may need to be overridden when mapping more complex types.

### Simple immutable classes

Consider a property the uses a value converter to map a simple, immutable class.

[!code-csharp[SimpleImmutableClass](../../../samples/core/Modeling/ValueConversions/Program.cs?name=SimpleImmutableClass)]

[!code-csharp[ConfigureImmutableClassProperty](../../../samples/core/Modeling/ValueConversions/Program.cs?name=ConfigureImmutableClassProperty)]

Properties of this type do not need special comparisons or snapshots because:
* Equality is overridden so that different instances will compare correctly
* The type is immutable, so there is no chance of mutating a snapshot value

So in this case the value converted mapping is fine as it is.

### Simple immutable Structs

The mapping for simple structs is also simple and requires no special comparers or snapshotting.

[!code-csharp[SimpleImmutableStruct](../../../samples/core/Modeling/ValueConversions/Program.cs?name=SimpleImmutableStruct)]

[!code-csharp[ConfigureImmutableStructProperty](../../../samples/core/Modeling/ValueConversions/Program.cs?name=ConfigureImmutableStructProperty)]

EF Core has built-in support for generating compiled, memberwise comparisons of struct properties.
This means structs don't need to have equality overridden for EF, but you may still choose to do this for other reasons.
Also, snapshotting is fine since structs are always memberwise copied.
(This is also true for mutable structs, but [mutable structs should in general be avoided](https://docs.microsoft.com/dotnet/csharp/write-safe-efficient-code).)

### Mutable classes

It is recommended that you use immutable types (classes or structs) with value converters when possible.
This is usually more efficient and has cleaner semantics than using a mutable type.   

However, that being said, it is common to use properties of types that the application cannot change.
For example, mapping a property containing a list of numbers: 

[!code-csharp[ListProperty](../../../samples/core/Modeling/ValueConversions/Program.cs?name=ListProperty)]

The [List class](https://docs.microsoft.com/dotnet/api/system.collections.generic.list-1?view=netstandard-2.1):
* Has reference equality; two lists containing the same values are treated as different.
* Is mutable; values in the list can be added, removed, and modified.

A typical value conversion on a list property might convert the list to and from JSON:

[!code-csharp[ConfigureListProperty](../../../samples/core/Modeling/ValueConversions/Program.cs?name=ConfigureListProperty)]

Then, to get correct comparisons, create and set a `ValueComparer<T>` on the same property:

[!code-csharp[ConfigureListPropertyComparer](../../../samples/core/Modeling/ValueConversions/Program.cs?name=ConfigureListPropertyComparer)]

> [!NOTE]  
> The model builder ("fluent") API to set a value comparer has not yet been implemented.
> Instead, the code above gets the lower-level `IMutableProperty` from the builder's `Metadata` property and sets the comparer directly.

The `ValueComparer<T>` constructor accepts three expressions:
* An expression for checking quality
* An expression for generating a hash code
* An expression to snapshot a value  

> [!INFO]  
> Value converters and comparers are constructed using expressions rather than simple delegates.
> This is because EF takes these expressions and inserts them inside a much more complex expression tree that is then compiled into an entity shaper delegate.
> Conceptually, this is similar to compiler inlining.
> For example, a simple conversion may just be a compiled in cast, rather than a call to another method to do the conversion.    

### Key comparers


### Overriding defaults
