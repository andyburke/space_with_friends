using UnityEngine;

namespace space_with_friends
{
    class utils
    {
        public static void Log(string msg, string subsystem = "" )
        {
            Debug.Log( $"[space_with_friends] { ( subsystem.Length > 0 ? "[[" + subsystem + "]]" : "" ) } {msg}" );
        }
    }
}
