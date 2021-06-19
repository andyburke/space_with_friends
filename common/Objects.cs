using System.Collections.Generic;
using System;

namespace space_with_friends {

	public class msg {
		public Guid id = Guid.NewGuid();
		public Guid world_id;
		public double world_time;
		public string type;
		public string source;
		public string target;
		public string message;
	}
}