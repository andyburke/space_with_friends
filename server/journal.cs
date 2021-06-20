using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;

namespace space_with_friends {

	public class JournalRecord {
		public Guid id = Guid.NewGuid();
		public Guid world_id = Guid.Empty;
		public double world_time;
		public Guid vessel_id = Guid.Empty;
		public string type;
		public string source;
		public string target;
		public string message;
	}

	public class Journal {
		private static readonly CaseInsensitiveComparer comparer = new CaseInsensitiveComparer();
		private static string[] JOURNAL_RECORD_FIELD_NAMES = null;
		private static ConcurrentDictionary< Guid, SqliteConnection > connections = new ConcurrentDictionary< Guid, SqliteConnection >();

		public static SqliteConnection _connect( Guid world_id ) {
			SqliteConnection existing_connection;
			connections.TryGetValue( world_id, out existing_connection );
			if ( existing_connection == null ) {
				if ( !Directory.Exists( "worlds" ) ) {
					Directory.CreateDirectory( "worlds" );
				}

				if ( !File.Exists( $"worlds/{ world_id }.db" ) ) {
					File.Create( $"worlds/{ world_id }.db" );
				}

				SqliteConnection new_connection = new SqliteConnection( $"Data Source=worlds/{ world_id }.db" );
 
 				SQLitePCL.Batteries.Init();
// 				SQLitePCL.raw.SetProvider( new SQLitePCL.SQLite3Provider_e_sqlite3() );

				connections.TryAdd( world_id, new_connection );
				existing_connection = new_connection;

				existing_connection.Open();

				var command = existing_connection.CreateCommand();
				command.Connection = existing_connection;
				command.CommandText =
				@"
					CREATE TABLE IF NOT EXISTS journal(
						id TEXT PRIMARY KEY,
						world_id TEXT,
						world_time REAL,
						vessel_id TEXT,
						type TEXT,
						source TEXT,
						target TEXT,
						message TEXT
					);
				";
				command.ExecuteNonQuery();
			}
			return existing_connection;
		}

		public static void insert( JournalRecord record ) {
			try {
				SqliteConnection connection = _connect( record.world_id );
				connection.Open();

				{
					if ( JOURNAL_RECORD_FIELD_NAMES == null ) {
						FieldInfo[] record_fields = record.GetType().GetFields();

						Array.Sort( record_fields, ( FieldInfo lhs, FieldInfo rhs ) => {
							return comparer.Compare( lhs.Name, rhs.Name );
						} );

						JOURNAL_RECORD_FIELD_NAMES = record_fields.Select( field => field.Name ).ToArray();
					}

					SqliteCommand command = new SqliteCommand();
					command.Connection = connection;
					command.CommandText =
						$"INSERT INTO journal( { JOURNAL_RECORD_FIELD_NAMES.Aggregate( ( total, next ) => total + ", " + next ) } ) " +
						$"VALUES( { JOURNAL_RECORD_FIELD_NAMES.Select( field => $"${ field }" ).Aggregate( ( total, next ) => total + ", " + next ) } )";

					foreach( var param_name in JOURNAL_RECORD_FIELD_NAMES ) {
						var parameter = command.CreateParameter();
						parameter.ParameterName = $"${ param_name }";
						command.Parameters.Add( parameter );
						var type = record.GetType();
						var field = type.GetField( param_name );
						var value = field.GetValue( record );
						switch( param_name ) {
							case "id":
							case "world_id":
							case "vessel_id":
								value = ((Guid)value).ToString();
								break;
						}
						parameter.Value = value ?? DBNull.Value;
					}

					command.ExecuteNonQuery();
				}
			}
			catch( Exception e ) {
				Console.Error.Write( e.ToString() );
			}
		}

		public static void close() {
			foreach( Guid world_id in connections.Keys ) {
				SqliteConnection connection = _connect( world_id );
				connection.Close();
				connections.TryRemove( world_id, out _ );
			}
		}
	}
}


