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
		public Guid world_id;
		public double world_time;
		public string source;
		public string target;
		public string message;
	}

	public class Journal {
		public static Type JournalRecord_Type = typeof( JournalRecord );

		private static readonly CaseInsensitiveComparer comparer = new CaseInsensitiveComparer();
		private static List<string> JOURNAL_RECORD_FIELD_NAMES = null;
		private static ConcurrentDictionary< Guid, SqliteConnection > connections = new ConcurrentDictionary< Guid, SqliteConnection >();

		public static SqliteConnection _connect( Guid world_id ) {
			SqliteConnection existing_connection;
			connections.TryGetValue( world_id, out existing_connection );
			if ( existing_connection == null ) {
				Directory.CreateDirectory( "worlds" );
				SqliteConnection new_connection = new SqliteConnection( $"Data Source=worlds/{ world_id }.db" );
				connections.TryAdd( world_id, new_connection );
				existing_connection = new_connection;

				var command = existing_connection.CreateCommand();
				command.CommandText =
				@"
					CREATE TABLE IF NOT EXISTS journal(
						id TEXT PRIMARY KEY,
						world_id TEXT,
						world_time REAL,
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
			// SqliteConnection connection = _connect( record.world_id );
			// connection.Open();

			// using (var transaction = connection.BeginTransaction())
			// using (var command = connection.CreateCommand())
			// {
			// 	if ( JOURNAL_RECORD_FIELD_NAMES == null ) {
			// 		Type record_type = typeof( JournalRecord );
			// 		FieldInfo[] record_fields = record_type.GetFields();

			// 		Array.Sort( record_fields, ( FieldInfo lhs, FieldInfo rhs ) => {
			// 			return comparer.Compare( lhs.Name, rhs.Name );
			// 		} );

			// 		JOURNAL_RECORD_FIELD_NAMES = (List<string>)( record_fields.Select( field => field.Name ) );
			// 	}

			// 	command.CommandText =
			// 		$"INSERT INTO journal( { JOURNAL_RECORD_FIELD_NAMES.Aggregate( ( total, next ) => total + ", " + next ) } ) " +
			// 		$"VALUES( { JOURNAL_RECORD_FIELD_NAMES.Select( field => $"${ field }" ).Aggregate( ( total, next ) => total + ", " + next ) } )";

			// 	foreach( var param_name in JOURNAL_RECORD_FIELD_NAMES ) {
			// 		var parameter = command.CreateParameter();
			// 		parameter.ParameterName = $"${ param_name }";
			// 		command.Parameters.Add( parameter );
			// 		parameter.Value = JournalRecord_Type.GetProperty( param_name ).GetValue( record, null ) ?? DBNull.Value;
			// 	}

			// 	command.ExecuteNonQuery();
			// 	transaction.Commit();
			// }
		}
	}
}


