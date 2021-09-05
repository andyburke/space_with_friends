using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;

using WindowsInput;
using WindowsInput.Native;

using UnityEngine;

namespace space_with_friends {

	[KSPAddon( KSPAddon.Startup.SpaceCentre, once: true )]
	public class FlightCtrlStateBridge : MonoBehaviour {

		static bool initialized = false;

		private Vessel active_vessel;
		private FlightCtrlState last_sent_state = new FlightCtrlState();

		private static Queue<Action> run_in_main = new Queue<Action>();

		public void Start() {
			if ( space_with_friends_settings.instance.host == "" ) {
				return;
			}

			if ( initialized ) {
				return;
			}

			utils.Log( "flightctrlstate bridge init" );

			DontDestroyOnLoad( this );

			Core.client.on_message += on_message;

			GameEvents.onVesselChange.Add( this.on_vessel_change );

			initialized = true;
		}

		void on_vessel_change( Vessel new_active_vessel ) {
			if ( active_vessel != null ) {
				active_vessel.OnFlyByWire -= this.on_fly_by_wire;
			}

			active_vessel = new_active_vessel;

			if ( active_vessel != null ) {
				active_vessel.OnFlyByWire += this.on_fly_by_wire;
			}
		}

		public void on_fly_by_wire( FlightCtrlState state ) {
			Kerbal active_kerbal = CameraManager.Instance.IVACameraActiveKerbal;
			if ( active_kerbal == null ) {
				return;
			}

			// only people sitting in pilot seats should send
			if ( ( active_kerbal.protoCrewMember?.seat?.seatTransformName?.IndexOf( "pilot", StringComparison.OrdinalIgnoreCase ) ?? -1 ) < 0 ) {
				return;
			}

			bool main_throttle_equal = state.mainThrottle == last_sent_state.mainThrottle;

			bool pitch_equal = state.pitch == last_sent_state.pitch;
			bool pitch_trim_equal = state.pitchTrim == last_sent_state.pitchTrim;

			bool roll_equal = state.roll == last_sent_state.roll;
			bool roll_trim_equal = state.rollTrim == last_sent_state.rollTrim;

			bool wheel_steer_equal = state.wheelSteer == last_sent_state.wheelSteer;
			bool wheel_steer_trim_equal = state.wheelSteerTrim == last_sent_state.wheelSteerTrim;

			bool wheel_throttle_equal = state.wheelThrottle == last_sent_state.wheelThrottle;
			bool wheel_throttle_trim_equal = state.wheelThrottleTrim == last_sent_state.wheelThrottleTrim;

			bool yaw_equal = state.yaw == last_sent_state.yaw;
			bool yaw_trim_equal = state.yawTrim == last_sent_state.yawTrim;

			bool x_equal = state.X == last_sent_state.X;
			bool y_equal = state.Y == last_sent_state.Y;
			bool z_equal = state.Z == last_sent_state.Z;

			if (
				main_throttle_equal &&

				pitch_equal &&
				pitch_trim_equal &&

				roll_equal &&
				roll_trim_equal &&

				wheel_steer_equal &&
				wheel_steer_trim_equal &&

				wheel_throttle_equal &&
				wheel_throttle_trim_equal &&

				yaw_equal &&
				yaw_trim_equal &&

				x_equal &&
				y_equal &&
				z_equal ) {
				return;
			}

			last_sent_state.CopyFrom( state );

			ConfigNode flight_ctrl_state_node = new ConfigNode( "root" );
			state.Save( flight_ctrl_state_node );

			Core.client?.send( new msg {
				world_id = space_with_friends.Core.world_id,
				source = space_with_friends.Core.player_id,
				world_time = Planetarium.GetUniversalTime(),
				vessel_id = FlightGlobals.ActiveVessel?.id ?? Guid.Empty,
				type = "flight_ctrl_state",
				message = flight_ctrl_state_node.toString()
			} );

		}

		void Update() {
			if ( !HighLogic.LoadedSceneIsFlight ) {
				return;
			}

			if ( FlightGlobals.ActiveVessel == null ) {
				return;
			}

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

		public void on_message( space_with_friends.msg msg ) {
			if ( !HighLogic.LoadedSceneIsFlight ) {
				return;
			}

			switch( msg.type ) {
				case "flight_ctrl_state":
					break;
				default:
					return;
			}

			if ( FlightGlobals.ActiveVessel?.id != msg.vessel_id ) {
				return;
			}

			ConfigNode flight_ctrl_state_node = ConfigNode.Parse( msg.message );
			FlightCtrlState state = new FlightCtrlState();
			state.Load( flight_ctrl_state_node );


			run_in_main.Enqueue( () => {
				FlightInputHandler.state.CopyFrom( state );
			} );
		}

		public void OnDestroy() {
			utils.Log( "flightctrlstate bridge stopping" );

			Core.client.on_message -= on_message;
			GameEvents.onVesselChange.Remove( this.on_vessel_change );

			if ( FlightGlobals.ActiveVessel != null ) {
				FlightGlobals.ActiveVessel.OnFlyByWire -= this.on_fly_by_wire;
			}

			active_vessel = null;

			initialized = false;
		}
	}
}
