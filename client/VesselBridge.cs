using FullSerializer;
using KSP.UI.Screens;

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

using UnityEngine;

namespace space_with_friends {

	public class VesselPosition {
		public double time;
		public OrbitSnapshot orbit_snapshot;
		public Vector3d position;
		public double latitude;
		public double longitude;
		public double altitude;
		public Quaternion rotation;
		public float height;
		public Vector3d normal;
		public Vector3d CoM;
	}

	[KSPAddon( KSPAddon.Startup.SpaceCentre, once: true )]
	public class VesselBridge : MonoBehaviour {
		private static readonly fsSerializer _serializer = new fsSerializer();

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

			GameEvents.onFlightReady.Add( this.on_flight_ready );
			GameEvents.onNewVesselCreated.Add( this.on_new_vessel_created );
			GameEvents.onVesselChange.Add( this.on_vessel_change );
			GameEvents.onVesselCreate.Add( this.on_vessel_create );
			GameEvents.onVesselDestroy.Add( this.on_vessel_destroy );
			GameEvents.onVesselLoaded.Add( this.on_vessel_loaded );
			GameEvents.onVesselRecovered.Add( this.on_vessel_recovered );
			GameEvents.OnVesselRollout.Add( this.on_vessel_rollout );
			GameEvents.onVesselTerminated.Add( this.on_vessel_terminated );
			GameEvents.onVesselWasModified.Add( this.on_vessel_was_modified );

			Core.client.on_message += on_message;
			initialized = true;
		}

		public void OnDestroy() {
			utils.Log( "vessel bridge stopping" );

			GameEvents.onVesselWasModified.Remove( this.on_vessel_was_modified );
			GameEvents.onVesselTerminated.Remove( this.on_vessel_terminated );
			GameEvents.OnVesselRollout.Remove( this.on_vessel_rollout );
			GameEvents.onVesselRecovered.Remove( this.on_vessel_recovered );
			GameEvents.onVesselLoaded.Remove( this.on_vessel_loaded );
			GameEvents.onVesselDestroy.Remove( this.on_vessel_destroy );
			GameEvents.onVesselCreate.Remove( this.on_vessel_create );
			GameEvents.onVesselChange.Remove( this.on_vessel_change );
			GameEvents.onNewVesselCreated.Remove( this.on_new_vessel_created );
			GameEvents.onFlightReady.Remove( this.on_flight_ready );

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

		public static ConcurrentDictionary< Guid, ProtoVessel > pending_vessels = new ConcurrentDictionary< Guid, ProtoVessel >();
		public static void on_message( space_with_friends.msg msg ) {
			switch( msg.type ) {
				case "vessel_rollout": {
					utils.Log( $"vessel_rollout: { msg.vessel_id }" );
					ProtoVessel created_protovessel = deserialize_protovessel( msg.message );
					Vessel existing_vessel = FlightGlobals.FindVessel( created_protovessel.vesselID );
					if ( existing_vessel != null ) {
						utils.Log( "  exists, skipping" );
						return;
					}

					bool pending = pending_vessels.TryAdd( msg.vessel_id, created_protovessel );
					if ( !pending ) {
						utils.Log( $"  ERROR: could not add { created_protovessel.vesselID.ToString() }" );
						return;
					}
				}
					break;
				case "vessel_position": {
					utils.Log( $"vessel_position: { msg.vessel_id }" );

					Vessel existing_vessel = FlightGlobals.Vessels.Find( v => v.id == msg.vessel_id );
					ProtoVessel created_protovessel = null;
					if ( !existing_vessel ) {
						utils.Log( $"  missing vessel: { msg.vessel_id }" );

						if ( !pending_vessels.TryGetValue( msg.vessel_id, out created_protovessel ) ) {
							utils.Log( $"  postiion update for non-existent, non-pending vessel: { msg.vessel_id }" );
							return;
						}
					}

					fsData data = fsJsonParser.Parse( msg.message );

					object deserialized = null;
					_serializer.TryDeserialize( data, typeof( VesselPosition ), ref deserialized ).AssertSuccess();
					VesselPosition vessel_position = (VesselPosition)deserialized;

					run_in_main.Enqueue( () => {

						if ( created_protovessel != null ) {
							created_protovessel.orbitSnapShot = vessel_position.orbit_snapshot;
							created_protovessel.rotation = vessel_position.rotation;
							created_protovessel.height = vessel_position.height;
							created_protovessel.normal = vessel_position.normal;

							utils.Log( "  creating pending vessel" );
							created_protovessel.Load( HighLogic.CurrentGame.flightState );
							utils.Log( "  loaded vessel" );

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
							else {
								created_protovessel.vesselRef.Load();
								existing_vessel = created_protovessel.vesselRef;

								foreach ( var crew in existing_vessel.GetVesselCrew() )
								{
									ProtoCrewMember._Spawn( crew );
									if ( crew.KerbalRef )
										crew.KerbalRef.state = Kerbal.States.ALIVE;
								}
							}
						}

						existing_vessel.latitude = vessel_position.latitude;
						existing_vessel.longitude = vessel_position.longitude;
						existing_vessel.altitude = vessel_position.altitude;
						existing_vessel.CoM = vessel_position.CoM;

						existing_vessel.orbitDriver.updateFromParameters();

						// if it's an asteroid, make sure we don't have too many
						bool is_rubble = vessel_utils.is_rubble( existing_vessel );

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
					} );
				}
					break;
				case "vessel_terminated": {
					utils.Log( $"removing vessel: ${ msg.vessel_id }" );
					Vessel existing_vessel = FlightGlobals.Vessels.Find( v => v.id == msg.vessel_id );
					if ( existing_vessel == null ) {
						utils.Log( "  doesn't exist, skipping" );
						return;
					}

					try
					{
						if ( FlightGlobals.fetch?.VesselTarget?.GetVessel().id == existing_vessel.id ) {
							FlightGlobals.fetch.SetVesselTarget( null );
						}

						FlightGlobals.RemoveVessel( existing_vessel );
						foreach ( var part in existing_vessel.parts ) {
							UnityEngine.Object.Destroy( part.gameObject );
						}
						UnityEngine.Object.Destroy( existing_vessel.gameObject );

						HighLogic.CurrentGame.flightState.protoVessels.RemoveAll( v => v?.vesselID == existing_vessel.id );
						KSCVesselMarkers.fetch?.RefreshMarkers();
					}
					catch ( Exception ex )
					{
						utils.Log( $"error destroying vessel: { ex }" );
						return;
					}

					utils.Log( $"  destroyed" );
				}
					break;
				case "vessel_create":
				case "vessel_loaded":
				case "vessel_recovered":
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

		public void on_flight_ready() {
			utils.Log( $"flight ready: { FlightGlobals.ActiveVessel?.id.ToString() }" );

			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = FlightGlobals.ActiveVessel?.id ?? Guid.Empty,
				type = "vessel_flight_ready",
				message = ""
			} );
		}

		public void on_new_vessel_created( Vessel vessel ) {
			utils.Log( $"new vessel created: { vessel.id.ToString() }" );
		}

		public void on_vessel_change( Vessel vessel ) {
			utils.Log( $"vessel change: { vessel.id.ToString() }" );

			ProtoVessel protovessel = vessel.protoVessel;
			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = vessel.id,
				type = "vessel_change",
				message = ""
			} );
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
				message = ""
			} );
		}

		public void on_vessel_destroy( Vessel vessel ) {
			utils.Log( $"vessel destroy: { vessel.id.ToString() }" );
			ProtoVessel protovessel = vessel.protoVessel;
			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = vessel.id,
				type = "vessel_destroy",
				message = ""
			} );
		}

		public void on_vessel_loaded( Vessel vessel ) {
			utils.Log( $"vessel loaded: { vessel.id.ToString() }" );
			ProtoVessel protovessel = vessel.protoVessel;
			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = vessel.id,
				type = "vessel_loaded",
				message = ""
			} );
		}

		public void on_vessel_recovered( ProtoVessel protovessel, bool arg ) {
			utils.Log( $"vessel recovered: { protovessel.vesselID.ToString() } { arg }" );

			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = protovessel.vesselID,
				type = "vessel_recovered",
				message = ""
			} );
		}

		public void on_vessel_rollout( ShipConstruct ship_construct ) {
			Vessel vessel = ship_construct.Parts?[ 0 ]?.vessel;

			utils.Log( $"vessel rollout: { ship_construct.shipName } { vessel?.id }" );
			utils.Log( $"  current active vessel: { FlightGlobals.ActiveVessel?.id }" );

			// TODO: set a timeout of a few ms, then get the active vessel and send it (or match something from the ship_construct to verify)
			// TODO: move vessel serialization here

			ProtoVessel protovessel = vessel?.BackupVessel();
			if ( protovessel == null ) {
				utils.Log( "  no protovessel" );
				return;
			}

			ConfigNode roster_node = new ConfigNode();
			HighLogic.CurrentGame.CrewRoster.Save( roster_node );
			string roster_message = roster_node.ToString();

			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = protovessel.vesselID,
				type = "roster",
				message = roster_message
			} );

			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = protovessel.vesselID,
				type = "vessel_rollout",
				message = serialize_protovessel( protovessel )
			} );

			OrbitSnapshot orbit_snapshot = protovessel.orbitSnapShot;
			Orbit orbit = protovessel.orbitSnapShot.Load();
			double now = Planetarium.GetUniversalTime();
			Vector3d position = orbit.getTruePositionAtUT( now );

			fsData data;
			_serializer.TrySerialize( typeof( VesselPosition ), new {
				time = now,
				orbit_snapshot = orbit_snapshot,
				position = position,
				latitude = protovessel.latitude,
				longitude = protovessel.longitude,
				altitude = protovessel.altitude,
				rotation = protovessel.rotation,
				height = protovessel.height,
				normal = protovessel.normal,
				CoM = protovessel.CoM
			}, out data ).AssertSuccess();
			string json = fsJsonPrinter.CompressedJson( data );

			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = protovessel.vesselID,
				type = "vessel_position",
				message = json
			} );
		}

		public void on_vessel_terminated( ProtoVessel protovessel ) {
			utils.Log( $"vessel terminated: { protovessel.vesselID.ToString() }" );

			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = protovessel.vesselID,
				type = "vessel_terminated",
				message = ""
			} );
		}

		public void on_vessel_was_modified( Vessel vessel ) {
			utils.Log( $"vessel was modified: { vessel.id.ToString() }" );
			ProtoVessel protovessel = vessel.protoVessel;
			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = vessel.id,
				type = "vessel_was_modified",
				message = ""
			} );
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
