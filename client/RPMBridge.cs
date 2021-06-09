using Newtonsoft.Json;

namespace space_with_friends {

#if !MARC

	[KSPAddon( KSPAddon.Startup.Flight, once: false )]
	public class RPMBridge {
		public void Start() {
			JSI.Core.Events.onEvent += on_event;
			Core.client.on_message += on_network_message;

			Core.client.broadcast( new msg.login { player_id="test" } );
		}

		public void OnDestroy() {
			Core.client.on_message -= on_network_message;
			JSI.Core.Events.onEvent -= on_event;
		}

		public static void on_event( JSI.Core.Event _event ) {
			switch( _event.type ) {
				case "click":
				case "release":
					Core.client.broadcast( new msg.rpm_event {
						player_id = space_with_friends.Core.player_id,
						event_json = JsonConvert.SerializeObject( _event )
					} );
					break;
			}
		}

		void on_network_message( object msg ) {
			if ( msg is msg.rpm_event rpm_event ) {
				try {
					JSI.Core.Event _event = JsonConvert.DeserializeObject<JSI.Core.Event>( rpm_event.event_json );

					switch( _event.type ) {
						case "click":
						case "release":
							break;
						default:
							return;
					}

					utils.Log( $"replay: {rpm_event.player_id}: { _event.type } { _event.vessel_id.ToString() } { ( (JSI.Core.EventData)_event.data ).propID } { ( (JSI.Core.EventData)_event.data ).buttonName }" );
					JSI.SmarterButton.Replay( _event );
				}
				catch (System.Exception ex) {
					UnityEngine.Debug.LogError( ex.StackTrace );
					return;
				}
			}
		}
	}

#endif
}

