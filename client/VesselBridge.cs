using KSP.UI.Screens;

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

using UnityEngine;

namespace space_with_friends {

	[KSPAddon( KSPAddon.Startup.SpaceCentre, once: true )]
	public class VesselBridge : MonoBehaviour {

		const int MAX_RUBBLE = 50;

		static bool initialized = false;

		static System.Random random = new System.Random();

		private static Queue<Action> run_in_main = new Queue<Action>();

		public void Start() {
			if ( space_with_friends_settings.instance.host == "" ) {
				return;
			}

			if ( initialized ) {
				return;
			}

			utils.Log( "vessel bridge starting" );

			DontDestroyOnLoad( this );

			GameEvents.onNewVesselCreated.Add( this.on_new_vessel_created );
			GameEvents.onVesselCreate.Add( this.on_vessel_create );
			GameEvents.onVesselDestroy.Add( this.on_vessel_destroy );
			GameEvents.onVesselLoaded.Add( this.on_vessel_loaded );
			GameEvents.onVesselRecovered.Add( this.on_vessel_recovered );
			GameEvents.onVesselTerminated.Add( this.on_vessel_terminated );
			GameEvents.onVesselWasModified.Add( this.on_vessel_was_modified );

			Core.client.on_message += on_message;
			initialized = true;
		}

		public void OnDestroy() {
			utils.Log( "vessel bridge stopping" );

			GameEvents.onVesselWasModified.Remove( this.on_vessel_was_modified );
			GameEvents.onVesselTerminated.Remove( this.on_vessel_terminated );
			GameEvents.onVesselRecovered.Remove( this.on_vessel_recovered );
			GameEvents.onVesselLoaded.Remove( this.on_vessel_loaded );
			GameEvents.onVesselDestroy.Remove( this.on_vessel_destroy );
			GameEvents.onVesselCreate.Remove( this.on_vessel_create );
			GameEvents.onNewVesselCreated.Remove( this.on_new_vessel_created );

			Core.client.on_message -= on_message;
			initialized = false;
		}

		void Update() {
			while( run_in_main.Count > 0 )
			{
				Action action = null;
				lock( run_in_main )
				{
					action = run_in_main.Dequeue();
				}
				action?.Invoke();
			}
		}

		public static void on_message( space_with_friends.msg msg ) {
			if ( !HighLogic.LoadedSceneIsFlight ) {
				return;
			}

			switch( msg.type ) {
				case "vessel_create":
					utils.Log( $"vessel_create: ${ msg.vessel_id }" );
					ProtoVessel created_protovessel = deserialize_protovessel( msg.message );
                    Vessel existing_vessel = FlightGlobals.FindVessel( created_protovessel.vesselID );
					if ( existing_vessel != null ) {
						utils.Log( "  exists, skipping" );
						return;
					}

					utils.Log( "  creating" );
					created_protovessel.Load( HighLogic.CurrentGame.flightState );

					// TODO: set position?

					created_protovessel.vesselRef.protoVessel = created_protovessel;
					if ( created_protovessel.vesselRef.isEVA )
					{
						var eva_module = created_protovessel.vesselRef.FindPartModuleImplementing<KerbalEVA>();
						if ( eva_module != null && eva_module.fsm != null && !eva_module.fsm.Started )
						{
							eva_module.fsm.StartFSM( "Idle (Grounded)" );
						}
						created_protovessel.vesselRef.GoOnRails();
					}

					created_protovessel.vesselRef.orbitDriver.updateFromParameters();

					created_protovessel.vesselRef.Load();
					created_protovessel.vesselRef.RebuildCrewList();
					created_protovessel.vesselRef.SpawnCrew();

					foreach ( var crew in created_protovessel.vesselRef.GetVesselCrew() )
					{
						ProtoCrewMember._Spawn( crew );
						if ( crew.KerbalRef )
							crew.KerbalRef.state = Kerbal.States.ALIVE;
					}

					// if it's an asteroid, make sure we don't have too many
					bool is_rubble = vessel_utils.is_rubble( created_protovessel );

					if ( is_rubble ) {
						Vessel[] rubble = FlightGlobals.Vessels.Where( v => vessel_utils.is_rubble( v ) ).ToArray();
						if ( rubble.Length > MAX_RUBBLE ) {
							int random_index = random.Next( 0, rubble.Length );
							Vessel to_be_destroyed = rubble[ random_index ];
							try
							{
								if ( FlightGlobals.fetch?.VesselTarget?.GetVessel().id == to_be_destroyed.id ) {
									FlightGlobals.fetch.SetVesselTarget( null );
								}

								FlightGlobals.RemoveVessel( to_be_destroyed );
								foreach ( var part in to_be_destroyed.parts ) {
									UnityEngine.Object.Destroy( part.gameObject );
								}
								UnityEngine.Object.Destroy( to_be_destroyed.gameObject );

								HighLogic.CurrentGame.flightState.protoVessels.RemoveAll( v => v?.vesselID == to_be_destroyed.id );
								KSCVesselMarkers.fetch?.RefreshMarkers();
							}
							catch ( Exception ex )
							{
								utils.Log( $"error removing rubble: { ex }" );
							}
						}
					}
					break;
				case "vessel_destroy":
				case "vessel_loaded":
				case "vessel_recovered":
				case "vessel_terminated":
				case "vessel_modified":
					break;
				default:
					return;
			}

		}

		// TODO: handle pilot ownership something like this:
		// ConfigNode pilot_node = new ConfigNode();
		// pilot_node.AddValue( "pilot", space_with_friends.Core.player_id );
		// vessel.AddNode( "swf_node", pilot_node );

		public void on_new_vessel_created( Vessel vessel ) {
			utils.Log( $"new vessel created: { vessel.id.ToString() }" );
		}

		public void on_vessel_create( Vessel vessel ) {
			utils.Log( $"vessel create: { vessel.id.ToString() }" );

			ProtoVessel protovessel = vessel.protoVessel;
			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = vessel.id,
				type = "vessel_create",
				message = serialize_protovessel( protovessel )
			} );
		}

		public void on_vessel_destroy( Vessel vessel ) {
			utils.Log( $"vessel destroy: { vessel.id.ToString() }" );
		}

		public void on_vessel_loaded( Vessel vessel ) {
			utils.Log( $"vessel loaded: { vessel.id.ToString() }" );
		}

		public void on_vessel_recovered( ProtoVessel vessel, bool arg ) {
			utils.Log( $"vessel recovered: { vessel.vesselID.ToString() } { arg }" );
		}

		public void on_vessel_terminated( ProtoVessel vessel ) {
			utils.Log( $"vessel terminated: { vessel.vesselID.ToString() }" );
		}

		public void on_vessel_was_modified( Vessel vessel ) {
			utils.Log( $"vessel was modified: { vessel.id.ToString() }" );
		}

		public static string serialize_protovessel( ProtoVessel protovessel ) {
			ConfigNode protovessel_node = new ConfigNode();
			protovessel.Save( protovessel_node );
			string protovessel_message = protovessel_node.ToString();
			return protovessel_message;
		}

		public static ProtoVessel deserialize_protovessel( string protovessel_message ) {
			ConfigNode protovessel_node = ConfigNode.Parse( protovessel_message );
			ProtoVessel protovessel = new ProtoVessel( protovessel_node, HighLogic.CurrentGame );
			return protovessel;
		}
	}
}
