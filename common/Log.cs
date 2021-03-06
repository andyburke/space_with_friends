using System;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
//using System.Threading.Tasks;

static public class log {


	[Flags]
	public enum LogType {
		Invalid = 0,
		Trace = 1,
		Debug = 2,
		Info = 3,
		High = 4,
		Warn = 5,
		Error = 6,
		Fatal = 7,
	}

	public struct LogEvent {
		public DateTime Time;
		public LogType LogType;
		public string Msg;
		public string Path;
		public int Line;
		public string Member;

		public string Cat;
		public object Obj;



		static ImmutableDictionary<string, string> s_shortname = ImmutableDictionary<string, string>.Empty;


		public LogEvent( LogType logType, string msg, string path, int line, string member, string cat, object obj ) {

			//Cache the automatic category names
			if (string.IsNullOrEmpty( cat )) {
				if (s_shortname.TryGetValue( path, out var autoCat )) {
					cat = autoCat;
				}
				else {
					var pathPieces = path.Split( '\\' );

					var lastDir = pathPieces[ pathPieces.Length - 2 ];

					ImmutableInterlocked.AddOrUpdate( ref s_shortname, path, lastDir, ( key, value ) => { return lastDir; } );

					cat = lastDir;
				}
			}

			Time = DateTime.Now;
			LogType = logType;
			Msg = msg;
			Path = path;
			Line = line;
			Member = member;
			Cat = cat;
			Obj = obj;
		}
	}

	public delegate void Log_delegate( LogEvent evt );



	static public void create( string filename ) {
		createLog( filename );
	}


	static public void destroy() {
		string msg = "==============================================================================\nLogfile shutdown at " + DateTime.Now.ToString();

		var evt = CreateLogEvent( LogType.Info, msg, "System", null );

		writeToAll( evt );

		stop();
	}


	static LogEvent CreateLogEvent( LogType logType, string msg, string cat, object obj, [CallerFilePath] string path = "", [CallerLineNumber] int line = -1, [CallerMemberName] string member = "" ) {
		var logEvent = new LogEvent( logType, msg, path, line, member, cat, obj );

		return logEvent;
	}




	// Forwards.
	static public void fatal( string msg, string cat = "", object obj = null, [CallerFilePath] string path = "", [CallerLineNumber] int line = -1, [CallerMemberName] string member = "" ) {
		logBase( msg, LogType.Fatal, path, line, member, cat, obj );
	}

	static public void error( string msg, string cat = "", object obj = null, [CallerFilePath] string path = "", [CallerLineNumber] int line = -1, [CallerMemberName] string member = "" ) {
		logBase( msg, LogType.Error, path, line, member, cat, obj );
	}

	static public void warn( string msg, string cat = "", object obj = null, [CallerFilePath] string path = "", [CallerLineNumber] int line = -1, [CallerMemberName] string member = "" ) {
		logBase( msg, LogType.Warn, path, line, member, cat, obj );
	}

	static public void info( string msg, string cat = "", object obj = null, [CallerFilePath] string path = "", [CallerLineNumber] int line = -1, [CallerMemberName] string member = "" ) {
		logBase( msg, LogType.Info, path, line, member, cat, obj );
	}

	static public void high( string msg, string cat = "", object obj = null, [CallerFilePath] string path = "", [CallerLineNumber] int line = -1, [CallerMemberName] string member = "" ) {
		logBase( msg, LogType.High, path, line, member, cat, obj );
	}

	static public void debug( string msg, string cat = "", object obj = null, [CallerFilePath] string path = "", [CallerLineNumber] int line = -1, [CallerMemberName] string member = "" ) {
		logBase( msg, LogType.Debug, path, line, member, cat, obj );
	}

	static public void trace( string msg, string cat = "", object obj = null, [CallerFilePath] string path = "", [CallerLineNumber] int line = -1, [CallerMemberName] string member = "" ) {
		logBase( msg, LogType.Trace, path, line, member, cat, obj );
	}

	static object s_lock = new object();

	static public void logBase( string msg, LogType type = LogType.Debug, string path = "", int line = -1, string member = "", string cat = "unk", object obj = null ) {
		// @@@@@ TODO Get rid of this lock. 
		var evt = new LogEvent( type, msg, path, line, member, cat, obj );

		lock (s_lock) {
			writeToAll( evt );
		}
	}

	static public void logProps( object obj, string header, LogType type = LogType.Debug, string cat = "", string prefix = "" ) {
		var list = refl.GetAllProperties( obj.GetType() );

		lock (s_lock) {
			var evt = CreateLogEvent( type, header, cat, obj );

			writeToAll( evt );

			foreach (var pi in list) {
				try {
					var v = pi.GetValue( obj );

					logBase( $"{prefix}{pi.Name} = {v}", type, cat );
				}
				catch (Exception ex) {
					logBase( $"Exception processing {pi.Name} {ex.Message}", LogType.Error, "log" );
				}
			}

		}
	}

	//This might seem a little odd, but the intent is that usually you wont need to set notExpectedValue. 
	static public void expected<T>( T value, string falseString, string trueString = "", T notExpectedValue = default( T ) ) {

		if (!value.Equals( notExpectedValue )) {
// FIXME: immutable issues
//			log.info( $"Properly got {value}{trueString}" );
		}
		else {
// FIXME: immutable issues
//			log.warn( $"Got {notExpectedValue} instead of {value}{falseString}" );
		}
	}


	static private void createLog( string filename ) {

		string dir = Path.GetDirectoryName( filename );

		if (dir.Length > 0) {
			Directory.CreateDirectory(dir);
		}

		s_stream = new FileStream( filename, FileMode.Append, FileAccess.Write );
		s_writer = new StreamWriter( s_stream );

		s_errorStream = new FileStream( filename + ".error", FileMode.Append, FileAccess.Write );
		s_errorWriter = new StreamWriter( s_errorStream );

		//Debug.Listeners.Add( this );

		string msg = "\n==============================================================================\nLogfile " + filename + " startup at " + DateTime.Now.ToString();

		var evt = CreateLogEvent( LogType.Info, msg, "System", null );

		writeToAll( evt );
	}

	/*
static public override void Write( string msg ) {
		WriteLine( msg );
	}

static public override void WriteLine( string msg ) {
		error( msg );
		//base.WriteLine( msg );
	}
	*/

	static void stop() {
		s_writer.Close();
		s_stream.Close();

		s_errorWriter.Close();
		s_errorStream.Close();
	}

	static public void addDelegate( Log_delegate cb ) {
		s_delegates.Add( cb );
	}

	public static char getSymbol( LogType type ) {
		switch (type) {
			case LogType.Trace:
				return ' ';
			case LogType.Debug:
				return ' ';
			case LogType.Info:
				return ' ';
			case LogType.High:
				return '+';
			case LogType.Warn:
				return '+';
			case LogType.Error:
				return '*';
			case LogType.Fatal:
				return '*';
			default:
				return '?';
		}
	}

	static private void writeToAll( LogEvent evt ) {
		try {
			// _SHOULDNT_ need this since we lock at the top.  
			//lock( this )
			{
				char sym = getSymbol( evt.LogType );

				var truncatedCat = evt.Cat.Substring( 0, Math.Min( 8, evt.Cat.Length ) );

				string finalLine = string.Format( "{0,-8}{1}| {2}", truncatedCat, sym, evt.Msg );

				//Console.WriteLine( finalMsg );
				//Console.Out.Write( finalMsg );

				s_writer.WriteLine( finalLine );

				Console.WriteLine( finalLine );

				Debug.WriteLine( finalLine );

				s_writer.Flush();

				foreach (Log_delegate cb in s_delegates) {
					{
						cb( evt );
					}
				}
			}
		}
		catch (Exception ex) {
			Console.WriteLine( "EXCEPTION DURING LOGGING" );
			Console.WriteLine( "EXCEPTION DURING LOGGING" );
			Console.WriteLine( "EXCEPTION DURING LOGGING" );
			Console.WriteLine( "EXCEPTION DURING LOGGING" );
			Console.WriteLine( "EXCEPTION DURING LOGGING" );
			Console.WriteLine( $"Exception {ex}" );

			Debug.WriteLine( "EXCEPTION DURING LOGGING" );
			Debug.WriteLine( "EXCEPTION DURING LOGGING" );
			Debug.WriteLine( "EXCEPTION DURING LOGGING" );
			Debug.WriteLine( "EXCEPTION DURING LOGGING" );
			Debug.WriteLine( "EXCEPTION DURING LOGGING" );
			Debug.WriteLine( $"Exception {ex}" );
		}
	}

	private static Stream s_stream;
	private static StreamWriter s_writer;

	private static Stream s_errorStream;
	private static StreamWriter s_errorWriter;

	private static ArrayList s_delegates = new ArrayList();









}
