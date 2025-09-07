# Contributing to Development

### [Build Instructions](https://github.com/XOWithSauce/schedule-cartelenforcer/edit/main/.github/BUILD.md)


### Code Principles
---
Contributions and changes should be made in identical functionality for both IL2Cpp and Mono. This needs to be done by using conditional expressions, example:

```cs
#if MONO
Do("Mono Stuff")
#else
Do("IL2Cpp Stuff")
#endif
```

Scenarios where to use the conditional expression:

1. Mono Backend can have the following type checking. This will cause errors in IL2Cpp Backend.
```cs
if (foo is Bar)
{
    foo.Run();
}
// Alternative
(foo as Bar).Run();
```
2. We produce IL2Cpp equivalent code to handle it using TryCast 
```cs
#if MONO
if (foo is Bar)
{
    foo.Run();
}
// Alternative
(foo as Bar).Run();
#else
Bar temp = foo.TryCast<Bar>();
if (temp != null)
{
    temp.Run();
}
#endif
```
---

If IntelliSense shows that there is a type mismatch between function return value in IL2Cpp and the Mono version, both versions types need to be respected and object management needs to be aware of the types or alternatively parse to a type which is supported by Mono.

Common Example where returned object could cause errors:
1. IL2Cpp : `Il2CppArray<Object>` 
2. Mono : `Object[]`. 

---

Coroutines must sleep in enumerations using the static declared WaitForSeconds objects, when applicable.

---

Code Changes should follow best practices of Unity and follow basic style Guidelines for C#.

Feature additions should take into account network behaviour and in future support multiplayer to some extent, pull requests may be declined due to no networking capability or support.

---

Pull Requests should have in the description:
- Short description of changed / added functionalities
- Are the changes tested, if yes then are they tested on Both IL2Cpp and Mono, or just one of these.
- If you added a new feature; Does the feature support networking behaviour (meaning code could be made to work in multiplayer)
- Attribution: Do you want to be attributed as source contributor in the releases and changelog
