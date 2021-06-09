using System.Collections.Generic;
using System;

namespace space_with_friends.msg {

	// M E S S A G E S
	public class login {
		public string player_id;
	}

	public class logout {
		public string player_id;
	}

	public class rpm_event {
		public string player_id;
		public string event_json;
	}



	// M E S S A G E  S U P P O R T
	// ???? Refactor these to an enum that the server doesnt care about
	public class SendToAll {
		public object Message;
	}

	public class SendToServer {
		public object Message;
	}

	public class SendToTarget {
		public string Target;
		public object Message;
	}

	public class SendFromTarget {
		public string Target;
		public object Message;
	}











	// D E B U G  S U P P O R T
	// D E B U G  S U P P O R T
	// D E B U G  S U P P O R T

	// This is like a login packet
	// Why do we have it? Just to show the most simple case
	public class ClientLogin {
		public string Name;
		public string Password;
	}


	// More complicated classes are possible as well
	// Like 'Person' which has references to other persons)
	public class Person {
		public string Name;
		public int Age;
		public List<Person> Friends = new List<Person>();
	}


	// Abstract classes and interfaces are very rarely handled
	// correctly with other serializers
	public interface ISpell {
		string Cast();
	}

	// A fireball that just deals some direct damage
	public class Fireball : ISpell {
		public int DamageMin = 40;
		public int DamageMax = 60;

		public string Cast() {
			var dmg = new Random().Next( DamageMin, DamageMax );
			return $"Fireball dealt {dmg} damage!";
		}
	}

	// Chain-lightning that jumps over many targets (losing damage with every jump)
	public class Lightning : ISpell {
		public int InitialDamage = 120;
		public float DamageFactorPerJump = 0.8f;
		public int MaxTargets = 6;

		public string Cast() {
			var rng = new Random();

			var numberOfTargets = rng.Next( MaxTargets / 2, MaxTargets );

			float totalDamage = 0;
			float currentDamage = InitialDamage;
			for (int i = 0; i < numberOfTargets; i++) {
				totalDamage += currentDamage;
				currentDamage *= DamageFactorPerJump;
			}

			return $"Lightning dealt {totalDamage:0.0} damage in total (jumped over {numberOfTargets} targets)!";
		}
	}





}