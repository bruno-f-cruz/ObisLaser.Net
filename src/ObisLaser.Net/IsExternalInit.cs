#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    // Enables C# records and init-only properties when targeting .NET Framework.
    internal static class IsExternalInit
    {
    }
}
#endif
