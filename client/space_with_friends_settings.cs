using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using KSP;

namespace space_with_friends {
	[KSPScenario( ScenarioCreationOptions.AddToAllGames, GameScenes.SPACECENTER, GameScenes.EDITOR, GameScenes.FLIGHT )]
	public class space_with_friends_settings : ScenarioModule {
		public string host = "";
		public UInt16 port = 7887;
		public Guid world_id = Guid.Empty;

		public static space_with_friends_settings instance;

		public static void MainMenuBuildDefaultScenarioModule() {
			if (instance == null) {
				instance = new space_with_friends_settings();
				utils.Log( "creating new settings module" );
				instance.OnLoad( new ConfigNode() );
				instance.Start();
			}
		}
		space_with_friends_settings() {
			instance = this;
		}

		void Start() {
			instance = this;
			GameEvents.onGameStateSave.Add(OnSave);
		}

		void OnDestroy() {
			//GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
			GameEvents.onGameStateSave.Remove(OnSave);
		}

		public override void OnSave( ConfigNode node ) {
			node.AddValue( "host", host );
			node.AddValue( "port", port.ToString() );
			node.AddValue( "world_id", world_id.ToString() );
		}

		public override void OnLoad( ConfigNode node ) {
			instance = this;

			if ( node.HasValue( "host" ) ) {
				host = node.GetValue( "host" );
			}

			if ( node.HasValue( "port" ) ) {
				port = UInt16.Parse( node.GetValue( "port" ) );
			}

			if ( node.HasValue( "world_id" ) ) {
				world_id = Guid.Parse( node.GetValue( "world_id" ) );
			}
			else {
				world_id = Guid.NewGuid();
			}

			utils.Log( "loaded host/port info from save game" );
			utils.Log( $"      host: { host }" );
			utils.Log( $"      port: { port }" );
			utils.Log( $"  world_id: { world_id.ToString() }" );
		}
	}
}