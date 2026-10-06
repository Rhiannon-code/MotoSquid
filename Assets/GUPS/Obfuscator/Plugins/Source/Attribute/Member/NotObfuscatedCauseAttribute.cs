using System;

namespace GUPS.Obfuscator.Attribute
{
    /// <summary>
    /// Do not use! Gets attached to not obfuscated members in developments builds.
    /// </summary>
    [AttributeUsage(AttributeTargets.All)]
    public class NotObfuscatedCauseAttribute : System.Attribute
    {
#pragma warning disable
        public NotObfuscatedCauseAttribute(String _Cause)
        {

        }
#pragma warning restore
    }
}