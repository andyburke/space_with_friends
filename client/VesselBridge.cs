using FullSerializer;
using KSP.UI.Screens;

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

using UnityEngine;

namespace space_with_friends {

	public class ProtoVesselMeta {
		public ConfigNode vessel_config_node;
		public ProtoVessel protovessel;
		public List< string > crewmembers;
	}

	public class VesselPosition {
		public double time;
		public string orbit_snapshot_string;
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

		public static ConcurrentDictionary< Guid, ProtoVesselMeta > pending_vessels = new ConcurrentDictionary< Guid, ProtoVesselMeta >();
		public static void on_message( space_with_friends.msg msg ) {
			switch( msg.type ) {
				case "vessel_rollout": {
					utils.Log( $"vessel_rollout: { msg.vessel_id }" );
					ProtoVesselMeta protovessel_meta = deserialize_protovessel( msg.message );

					if ( protovessel_meta.protovessel == null ) {
						return;
					}

					Vessel existing_vessel = FlightGlobals.FindVessel( protovessel_meta.protovessel.vesselID );
					if ( existing_vessel != null ) {
						utils.Log( "  exists, skipping" );
						return;
					}

					bool pending = pending_vessels.TryAdd( msg.vessel_id, protovessel_meta );
					if ( !pending ) {
						utils.Log( $"  ERROR: could not add { protovessel_meta.protovessel.vesselID.ToString() }" );
						return;
					}
				}
					break;
				case "vessel_position": {
					utils.Log( $"vessel_position: { msg.vessel_id }" );

					Vessel existing_vessel = FlightGlobals.Vessels.Find( v => v.id == msg.vessel_id );
					ProtoVesselMeta protovessel_meta = null;
					if ( !existing_vessel ) {
						utils.Log( $"  missing vessel: { msg.vessel_id }" );

						if ( !pending_vessels.TryGetValue( msg.vessel_id, out protovessel_meta ) ) {
							utils.Log( $"  postiion update for non-existent, non-pending vessel: { msg.vessel_id }" );
							return;
						}
					}

					fsData data = fsJsonParser.Parse( msg.message );

					object deserialized = null;
					_serializer.TryDeserialize( data, typeof( VesselPosition ), ref deserialized ).AssertSuccess();
					VesselPosition vessel_position = (VesselPosition)deserialized;

					// TODO: if vessel_position.time is too far in the past, ignore?

					run_in_main.Enqueue( () => {

						ConfigNode orbit_snapshot_node = ConfigNode.Parse( vessel_position.orbit_snapshot_string ).nodes[ 0 ];
 						utils.Log( orbit_snapshot_node.ToString() );

						OrbitSnapshot orbit_snapshot = new OrbitSnapshot( orbit_snapshot_node );
						Orbit orbit = orbit_snapshot.Load();

						if ( protovessel_meta != null ) {

							// utils.Log( "orbit_snapshot_node" );
							// utils.Log( orbit_snapshot_node.ToString() );
							protovessel_meta.protovessel.orbitSnapShot = orbit_snapshot;

							protovessel_meta.protovessel.rotation = vessel_position.rotation;
							protovessel_meta.protovessel.height = vessel_position.height;
							protovessel_meta.protovessel.normal = vessel_position.normal;

// need to set crew indexes in the global roster?
							utils.Log( "  creating pending vessel" );

							bool can_spawn_vessel = true;
							foreach ( ProtoPartSnapshot snapshot in protovessel_meta.protovessel.protoPartSnapshots )
							{
								if ( snapshot.partInfo == null )
								{
									utils.Log( $"  ERROR: protovessel { protovessel_meta.protovessel.vesselName } has missing part '{ snapshot.partName }': skipping load" );
									can_spawn_vessel = false;
									break;
								}

								foreach ( ProtoPartResourceSnapshot resource in snapshot.resources )
								{
									if ( !PartResourceLibrary.Instance.resourceDefinitions.Contains( resource.resourceName ) )
									{
										utils.Log( $"  ERROR: protovessel { protovessel_meta.protovessel.vesselName } has missing resource '{ resource.resourceName }': skipping load" );
										can_spawn_vessel = false;
										break;
									}
								}
							}

							List< ProtoCrewMember > crew = protovessel_meta.protovessel.GetVesselCrew();
							if ( crew.Count != protovessel_meta.crewmembers.Count ) {
								utils.Log( $"  ERROR: protovessel crew count: { crew.Count } / incoming crew count: { protovessel_meta.crewmembers.Count }" );
								can_spawn_vessel = false;
							}

							if ( protovessel_meta.protovessel.protoPartSnapshots == null ) {
								utils.Log( $"  ERROR: protovessel protoPartSnapshots is null" );
								can_spawn_vessel = false;
							}
							else {
								for ( int index = 0; index < protovessel_meta.protovessel.protoPartSnapshots.Count; ++index ) {
									if ( protovessel_meta.protovessel.protoPartSnapshots[ index ] == null ) {
										utils.Log( $"  ERROR: protovessel protoPartSnapshots[ { index } ] is null" );
										can_spawn_vessel = false;
										break;
									}
									if ( protovessel_meta.protovessel.protoPartSnapshots[ index ].partName == null ) {
										utils.Log( $"  ERROR: protovessel protoPartSnapshots[ { index } ].partName is null" );
										can_spawn_vessel = false;
										break;
									}
								}
							}

							if ( protovessel_meta.protovessel.orbitSnapShot == null ) {
								utils.Log( $"  ERROR: protovessel orbitSnapShot is null" );
								can_spawn_vessel = false;
							}

							if ( protovessel_meta.protovessel.OverrideDefault == null ) {
								utils.Log( $"  ERROR: protovessel OverrideDefault is null" );
								can_spawn_vessel = false;
							}

							if ( protovessel_meta.protovessel.OverrideActionControl == null ) {
								utils.Log( $"  ERROR: protovessel OverrideActionControl is null" );
								can_spawn_vessel = false;
							}

							if ( protovessel_meta.protovessel.OverrideAxisControl == null ) {
								utils.Log( $"  ERROR: protovessel OverrideAxisControl is null" );
								can_spawn_vessel = false;
							}

							if ( protovessel_meta.protovessel.OverrideGroupNames == null ) {
								utils.Log( $"  ERROR: protovessel OverrideGroupNames is null" );
								can_spawn_vessel = false;
							}

							if ( !can_spawn_vessel ) {
								return;
							}

							utils.Log( "  adding protovessel to flightstate" );
							HighLogic.CurrentGame.flightState.protoVessels.Add( protovessel_meta.protovessel );

							int crew_index = 0;
							foreach ( string crewmember_name in protovessel_meta.crewmembers ) {
								if ( !HighLogic.CurrentGame.CrewRoster.Exists( crewmember_name ) )
								{
									ProtoCrewMember crewmember = CrewGenerator.RandomCrewMemberPrototype( ProtoCrewMember.KerbalType.Crew );
									HighLogic.CurrentGame.CrewRoster.AddCrewMember( crewmember );
									crewmember.ChangeName( crewmember_name );
									crewmember.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
									utils.Log( $"  WARN: generated a missing kerbal [{ crewmember.name }] from vessel { protovessel_meta.protovessel.vesselName }" );
								}

								HighLogic.CurrentGame.CrewRoster[ crewmember_name ].rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
								HighLogic.CurrentGame.CrewRoster[ crewmember_name ].seatIdx = crew_index;
								++crew_index;
							}

							try {
								// TODO: should we not be actually loading it if they're in the wrong scene/too far away?
								protovessel_meta.protovessel.Load( HighLogic.CurrentGame.flightState );
								utils.Log( "  loaded vessel" );

								if ( protovessel_meta.protovessel.vesselRef == null ) {
									utils.Log( "  protovessel.vesselRef is null!" );
									return;
								}
							}
							catch( Exception ex ) {
								utils.Log( "EXCEPTION LOADING PROTOVESSEL" );
								utils.Log( ex.ToString() );
								return;
							}

							protovessel_meta.protovessel.vesselRef.protoVessel = protovessel_meta.protovessel;
							if ( protovessel_meta.protovessel.vesselRef.isEVA ) {
								var eva_module = protovessel_meta.protovessel.vesselRef.FindPartModuleImplementing<KerbalEVA>();
								if ( eva_module != null && eva_module.fsm != null && !eva_module.fsm.Started )
								{
									eva_module.fsm.StartFSM( "Idle (Grounded)" );
								}
								protovessel_meta.protovessel.vesselRef.GoOnRails();
							}
							else {
								existing_vessel = protovessel_meta.protovessel.vesselRef;

								foreach ( var crewmember in existing_vessel.GetVesselCrew() )
								{
									ProtoCrewMember._Spawn( crewmember );
									if ( crewmember.KerbalRef )
										crewmember.KerbalRef.state = Kerbal.States.ALIVE;
								}
							}
						}

						if ( existing_vessel == null ) {
							utils.Log( "  no existing vessel!" );
							return;
						}

						// existing_vessel.position = vessel_position.position;
						existing_vessel.latitude = vessel_position.latitude;
						existing_vessel.longitude = vessel_position.longitude;
						existing_vessel.altitude = vessel_position.altitude;
						existing_vessel.srfRelRotation = vessel_position.rotation;
						existing_vessel.heightFromTerrain = vessel_position.height;
						existing_vessel.terrainNormal = vessel_position.normal;
						existing_vessel.CoM = vessel_position.CoM;

						utils.Log( $"  existing_vessel.situation: { existing_vessel.situation }" );
						utils.Log( $"  loaded: { existing_vessel.loaded }" );
						utils.Log( $"  packed: { existing_vessel.packed }" );
						utils.Log( $"  Landed: { existing_vessel.Landed }" );
						utils.Log( $"  Splashed: { existing_vessel.Splashed }" );
						utils.Log( $"  latitude: { existing_vessel.latitude }" );
						utils.Log( $"  longitude: { existing_vessel.longitude }" );
						utils.Log( $"  altitude: { existing_vessel.altitude }" );
						utils.Log( $"  srfRelRotation: { existing_vessel.srfRelRotation }" );
						utils.Log( $"  heightFromTerrain: { existing_vessel.heightFromTerrain }" );
						utils.Log( $"  terrainNormal: { existing_vessel.terrainNormal }" );
						utils.Log( $"  CoM: { existing_vessel.CoM }" );

						utils.Log( "  orbit:" );
						utils.Log( $"    inclination: { orbit.inclination }" );
						utils.Log( $"    eccentricity: { orbit.eccentricity }" );
						utils.Log( $"    semiMajorAxis: { orbit.semiMajorAxis }" );
						utils.Log( $"    LAN: { orbit.LAN }" );
						utils.Log( $"    argumentOfPeriapsis: { orbit.argumentOfPeriapsis }" );
						utils.Log( $"    meanAnomalyAtEpoch: { orbit.meanAnomalyAtEpoch }" );
						utils.Log( $"    epoch: { orbit.epoch }" );
						utils.Log( $"    referenceBody: { orbit.referenceBody }" );

						existing_vessel.terrainAltitude = -1.0;
						utils.Log( $"  terrainAltitude: { existing_vessel.terrainAltitude } (pre-orbit update)" );

						existing_vessel.orbit.SetOrbit( orbit.inclination, orbit.eccentricity, orbit.semiMajorAxis, orbit.LAN, orbit.argumentOfPeriapsis, orbit.meanAnomalyAtEpoch, orbit.epoch, orbit.referenceBody );
						existing_vessel.orbitDriver.orbit.UpdateFromOrbitAtUT( existing_vessel.orbitDriver.orbit, Planetarium.GetUniversalTime(), orbit.referenceBody );
						existing_vessel.orbitDriver.updateFromParameters();

						// existing_vessel.Load();

						// existing_vessel.UpdatePosVel();

						// if ( existing_vessel.situation < Vessel.Situations.FLYING ) {
						// 	existing_vessel.altitude = vessel_position.altitude;
						// 	existing_vessel.terrainAltitude = -1.0;
						// }

						utils.Log( "  (after load + orbit update)" );
						utils.Log( $"  altitude: { existing_vessel.altitude }" );
						utils.Log( $"  terrainAltitude: { existing_vessel.terrainAltitude }" );

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

						utils.Log( "vessel position updated" );
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
			//ProtoVessel protovessel = vessel?.protoVessel;

			if ( protovessel == null ) {
				utils.Log( "  no protovessel" );
				return;
			}

			ConfigNode roster_node = new ConfigNode();
			HighLogic.CurrentGame.CrewRoster.Save( roster_node );
			string roster_message = roster_node.ToString();

			OrbitSnapshot orbit_snapshot = protovessel.orbitSnapShot;
			Orbit orbit = protovessel.orbitSnapShot.Load();
			double now = Planetarium.GetUniversalTime();
			Vector3d position = orbit.getTruePositionAtUT( now );

			utils.Log( "  sending roster" );
			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = protovessel.vesselID,
				type = "roster",
				message = roster_message
			} );

			utils.Log( "  sending vessel_rollout" );
			string vessel_rollout_message = serialize_protovessel( protovessel );
			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = protovessel.vesselID,
				type = "vessel_rollout",
				message = vessel_rollout_message
			} );

			fsData data;
			ConfigNode orbit_node = new ConfigNode( "root" );
			orbit_snapshot.Save( orbit_node );

			utils.Log( orbit_snapshot.ToString() );

			VesselPosition vessel_position = new VesselPosition();
			vessel_position.time = now;
			vessel_position.orbit_snapshot_string = orbit_node.ToString();
			vessel_position.position = position;
			vessel_position.latitude = protovessel.latitude;
			vessel_position.longitude = protovessel.longitude;
			vessel_position.altitude = protovessel.altitude;
			vessel_position.rotation = protovessel.rotation;
			vessel_position.height = protovessel.height;
			vessel_position.normal = protovessel.normal;
			vessel_position.CoM = protovessel.CoM;

			_serializer.TrySerialize( typeof( VesselPosition ), vessel_position, out data ).AssertSuccess();
			string json = fsJsonPrinter.CompressedJson( data );

			utils.Log( "  sending vessel_position" );
			utils.Log( json );
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

		public static ProtoVesselMeta deserialize_protovessel( string protovessel_message ) {
			ProtoVesselMeta result = new ProtoVesselMeta();


			ConfigNode parsed = ConfigNode.Parse( protovessel_message );
			if ( parsed == null || parsed.nodes.Count != 1 ) {
				utils.Log( "ERROR: could not deserialize protovessel" );
				return result;
			}

			result.vessel_config_node = parsed.nodes[ 0 ];
			if ( result.vessel_config_node == null ) {
				utils.Log( "ERROR: vessel node is missing/empty" );
				return result;
			}

			// utils.Log( "parsed > string:" );
			// utils.Log( result.vessel_config_node.ToString() );

			// utils.Log( "parsed incoming ship nodes:" );
			// foreach ( ConfigNode node in result.vessel_config_node.nodes ) {
			// 	utils.Log( $"  { node.name }" );
			// }

			clean_protovessel_nodes( result );

			result.protovessel = new ProtoVessel( result.vessel_config_node, HighLogic.CurrentGame );

			ensure_orbit_snapshot( result );

			return result;
		}

		public static string get_updated_time_value( string input ) {
			string value = input.Substring( 0, input.IndexOf( ", ", StringComparison.Ordinal ) );
			string time = input.Substring( input.IndexOf( ", ", StringComparison.Ordinal) + 1 );
			double updated_time = Math.Min( Double.Parse( time ), Planetarium.GetUniversalTime() );
			return $"{ value }, { updated_time }";
		}

		public static string[] CLEAN_NODE_TYPES = {
			"ACTIONGROUPS"
		};

		public static void clean_protovessel_nodes( ProtoVesselMeta protovessel_meta ) {
			foreach( string node_type in CLEAN_NODE_TYPES ) {
				ConfigNode node = protovessel_meta.vessel_config_node.GetNode( node_type );
				if ( node == null ) {
					continue;
				}

				foreach ( string key in node.values.DistinctNames() )
				{
					node.SetValue( key, get_updated_time_value( node.GetValue( key ) ) );
				}
			}

			protovessel_meta.crewmembers = new List< string >();
			foreach ( ConfigNode node in protovessel_meta.vessel_config_node.GetNodes( "PART" ) ) {
				foreach ( string name in node.GetValues( "crew" ) ) {
					protovessel_meta.crewmembers.Add( name );
				}
			}

			// string situation = protovessel_meta.vessel_config_node.GetValue( "sit" );
			// protovessel_meta.vessel_config_node.SetValue( "landed", situation == "LANDED" ? "True" : "False" );
			// protovessel_meta.vessel_config_node.SetValue( "splashed", situation == "SPLASHED" ? "True" : "False" );
		}

		public static void ensure_orbit_snapshot( ProtoVesselMeta protovessel_meta ) {
			utils.Log( "ensure_orbit_snapshot" );
			if ( protovessel_meta.protovessel.orbitSnapShot == null ) {
				utils.Log( "  orbitSnapShot is null" );
				foreach ( ConfigNode node in protovessel_meta.vessel_config_node.nodes ) {
					utils.Log( $"  { node.name }" );
				}
				ConfigNode orbit_node = protovessel_meta.vessel_config_node.GetNode( "ORBIT" );
				if ( orbit_node == null ) {
					utils.Log( "  orbit_node is null" );
					throw new Exception( "received protovessel without Orbit node" );
				}

				utils.Log( "  creating OrbitSnapshot from orbit node" );
				protovessel_meta.protovessel.orbitSnapShot = new OrbitSnapshot( orbit_node );

				// if ( orbit_node != null ) {
				// 	protovessel_meta.protovessel.orbitSnapShot.semiMajorAxis = orbit_node.GetValue( "SMA" );
				// 	protovessel_meta.protovessel.orbitSnapShot.eccentricity = orbit_node.GetValue( "ECC" );
				// 	protovessel_meta.protovessel.orbitSnapShot.inclination = orbit_node.GetValue( "INC" );
				// 	protovessel_meta.protovessel.orbitSnapShot.argOfPeriapsis = orbit_node.GetValue( "LPE" );
				// 	protovessel_meta.protovessel.orbitSnapShot.LAN = orbit_node.GetValue( "LAN" );
				// 	protovessel_meta.protovessel.orbitSnapShot.meanAnomalyAtEpoch = orbit_node.GetValue( "MNA" );
				// 	protovessel_meta.protovessel.orbitSnapShot.epoch = orbit_node.GetValue( "EPH" );
				// 	protovessel_meta.protovessel.orbitSnapShot.ReferenceBodyIndex = orbit_node.GetValue( "REF" );
				// }
			}

			utils.Log( "  done ensuring orbit snapshot" );
		}
	}
}
