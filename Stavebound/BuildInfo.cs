namespace Stavebound
{
    /// <summary>
    /// Assembly identity. These have to be compile-time constants because they are used in
    /// attribute arguments, so they cannot be read from the csproj — keep <see cref="Version"/>
    /// in step with the &lt;Version&gt; property in Stavebound.csproj when releasing.
    /// </summary>
    internal static class BuildInfo
    {
        internal const string Guid = "com.recognizerhd.stavebound";
        internal const string Name = "Stavebound";
        internal const string Version = "0.1.0";
    }
}
