using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BASeCamp.Elementizer
{
    public class Elementizer
    {
        /// <summary> Return the git hash value for this build.</summary>
        public static string GetGitHash()
        {
            var asm = Assembly.GetExecutingAssembly();
            var attrs = asm.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute),false);
            foreach (var iterate in attrs)
            {
                if (iterate is AssemblyInformationalVersionAttribute asatr)
                {
                    if (asatr.InformationalVersion.StartsWith("GitHash;"))
                        return asatr.InformationalVersion.Substring(8);

                }
            }
            return null;
        }
    }
}
