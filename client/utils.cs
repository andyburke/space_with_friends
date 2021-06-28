using UnityEngine;

namespace space_with_friends
{
	class utils {
		public static void Log(string msg, string subsystem = "" )
		{
			Debug.Log( $"[space_with_friends] { ( subsystem.Length > 0 ? "[[" + subsystem + "]]" : "" ) } {msg}" );
		}
	}

	class vessel_utils {
		public static bool is_rubble( Vessel v ) {
			return v.vesselName.StartsWith( "Ast." );
		}
		public static bool is_rubble( ProtoVessel v ) {
			return v.vesselName.StartsWith( "Ast." );
		}

		public string type( Vessel v ) {
			switch ( v.protoVessel?.protoPartSnapshots?[ 0 ]?.partName ) {
				case "PotatoRoid":
					return "asteroid";
				case "PotatoComet":
					return "comet";
				default:
					return "unknown";
			}
		}
	}
}
