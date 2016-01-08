﻿using System.Collections.Generic;

namespace NScientist
{
	public class Results
	{
		public Observation Control { get; set; }
		public Observation Trial { get; set; }

		public string Name { get; set; }
		public Dictionary<object, object> Context { get; set; }
		public bool ExperimentEnabled { get; set; }
		public bool Matched { get; set; }

		public Results()
		{
			Matched = true;
			Context = new Dictionary<object, object>();
		}
	}
}
