namespace space_with_friends {
	class VesselUtils {
		static public void process_incoming_kerbals( ConfigNode vessel_node, Guid protovessel_id )
		{
			List<string> missing_kerbals;

			foreach ( ConfigNode part_node in vessel_node.GetNodes( "PART" ) )
			{
				int crew_index = 0;
				foreach ( string kerbal_name in part_node.GetValues( "crew" ) )
				{
				}
			}
		}
	}
}


		List<string> takenKerbals = new List<string>();
		foreach (ConfigNode partNode in inputNode.GetNodes("PART"))
		{
			int crewIndex = 0;
			foreach (string currentKerbalName in partNode.GetValues("crew"))
			{
				if (kerbalToVessel.ContainsKey(currentKerbalName) ? kerbalToVessel[currentKerbalName] != protovesselID : false)
				{
					ProtoCrewMember newKerbal = null;
					ProtoCrewMember.Gender newKerbalGender = GetKerbalGender(currentKerbalName);
					string newExperienceTrait = null;
					if (HighLogic.CurrentGame.CrewRoster.Exists(currentKerbalName))
					{
						ProtoCrewMember oldKerbal = HighLogic.CurrentGame.CrewRoster[currentKerbalName];
						newKerbalGender = oldKerbal.gender;
						newExperienceTrait = oldKerbal.experienceTrait.TypeName;
					}
					foreach (ProtoCrewMember possibleKerbal in HighLogic.CurrentGame.CrewRoster.Crew)
					{
						bool kerbalOk = true;
						if (kerbalOk && kerbalToVessel.ContainsKey(possibleKerbal.name) && (takenKerbals.Contains(possibleKerbal.name) || kerbalToVessel[possibleKerbal.name] != protovesselID))
						{
							kerbalOk = false;
						}
						if (kerbalOk && possibleKerbal.gender != newKerbalGender)
						{
							kerbalOk = false;
						}
						if (kerbalOk && newExperienceTrait != null && possibleKerbal.experienceTrait != null && newExperienceTrait != possibleKerbal.experienceTrait.TypeName)
						{
							kerbalOk = false;
						}
						if (kerbalOk)
						{
							newKerbal = possibleKerbal;
							break;
						}
					}
					int kerbalTries = 0;
					while (newKerbal == null)
					{
						bool kerbalOk = true;
						ProtoCrewMember.KerbalType kerbalType = ProtoCrewMember.KerbalType.Crew;
						if (newExperienceTrait == "Tourist")
						{
							kerbalType = ProtoCrewMember.KerbalType.Tourist;
						}
						if (newExperienceTrait == "Unowned")
						{
							kerbalType = ProtoCrewMember.KerbalType.Unowned;
						}
						ProtoCrewMember possibleKerbal = HighLogic.CurrentGame.CrewRoster.GetNewKerbal(kerbalType);
						if (kerbalTries < 200 && possibleKerbal.gender != newKerbalGender)
						{
							kerbalOk = false;
						}
						if (kerbalTries < 100 && newExperienceTrait != null && newExperienceTrait != possibleKerbal.experienceTrait.TypeName)
						{
							kerbalOk = false;
						}
						if (kerbalOk)
						{
							newKerbal = possibleKerbal;
						}
						kerbalTries++;
					}
					DarkLog.Debug("Generated dodged kerbal with " + kerbalTries + " tries");
					partNode.SetValue("crew", newKerbal.name, crewIndex);
					newKerbal.seatIdx = crewIndex;
					newKerbal.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
					takenKerbals.Add(newKerbal.name);
				}
				else
				{
					takenKerbals.Add(currentKerbalName);
					CreateKerbalIfMissing(currentKerbalName, protovesselID);
					HighLogic.CurrentGame.CrewRoster[currentKerbalName].rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
					HighLogic.CurrentGame.CrewRoster[currentKerbalName].seatIdx = crewIndex;
				}
				crewIndex++;
			}
		}
		vesselToKerbal[protovesselID] = takenKerbals;
		foreach (string name in takenKerbals)
		{
			kerbalToVessel[name] = protovesselID;
		}
	}

}