using System.Collections.Generic;
using System;

namespace space_with_friends {

	public class msg {
		public Guid id = Guid.NewGuid();
		public Guid world_id = Guid.Empty;
		public double world_time;
		public Guid vessel_id = Guid.Empty;
		public string type;
		public string source;
		public string target;
		public string message;
	}
}