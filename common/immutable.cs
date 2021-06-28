﻿using System;
using System.Collections.Immutable;
using System.Threading;

namespace space_with_friends {


	public static class imm {



		public static int add<T>( T val, ref ImmutableList<T> orig ) {
			bool retry;
			int count = 0;
			do {
				var snapshot = orig;
				var combined = snapshot.Add( val );
				retry = Interlocked.CompareExchange( ref orig, combined, snapshot )
						  != snapshot;
				++count;

				if ( count > 100 ) {
					throw new Exception( "could not add to immutable list" );
				}
			} while (retry);

			return count;
		}

		public static int remove<T>( T val, ref ImmutableList<T> orig ) {
			bool retry;
			int count = 0;
			do {
				var snapshot = orig;
				var combined = snapshot.Remove( val );
				retry = Interlocked.CompareExchange( ref orig, combined, snapshot )
						  != snapshot;
				++count;

				if ( count > 100 ) {
					throw new Exception( "could not remove from immutable list" );
				}
			} while (retry);

			return count;
		}


	}



}
