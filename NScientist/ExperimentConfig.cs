using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NScientist
{
	public class ExperimentConfig<TResult>
	{


		private Func<bool> _isEnabled;
		private Action<Results> _publish;
		private Func<TResult, TResult, bool> _compare;
		private Func<Dictionary<object, object>> _createContext;
		private Func<TResult, object> _cleaner;
		private bool _throwMismatches;
		private bool _parallel;


		private readonly Trial<TResult> _control;
		private readonly List<Func<TResult, TResult, bool>> _ignores;
		private readonly List<Trial<TResult>> _tests;

		public ExperimentConfig(Func<TResult> action)
		{
			_control = new Trial<TResult>(action) { TrialName = "Unnamed Experiment" };
			_tests = new List<Trial<TResult>>();
			_ignores = new List<Func<TResult, TResult, bool>>();

			_isEnabled = () => true;
			_publish = results => { };
			_compare = (control, experiment) => Equals(control, experiment);
			_createContext = () => new Dictionary<object, object>();
			_cleaner = results => null;
			_throwMismatches = false;
			_parallel = false;
		}

		public ExperimentConfig<TResult> Try(Func<TResult> action)
		{
			return Try("Trial " + _tests.Count, action);
		}

		public ExperimentConfig<TResult> Try(string trialName, Func<TResult> action)
		{
			_tests.Add(new Trial<TResult>(action) { TrialName = trialName });
			return this;
		}

		public ExperimentConfig<TResult> Enabled(Func<bool> isEnabled)
		{
			_isEnabled = isEnabled;
			return this;
		}

		public ExperimentConfig<TResult> CompareWith(Func<TResult, TResult, bool> compare)
		{
			_compare = compare;
			return this;
		}

		public ExperimentConfig<TResult> Ignore(Func<TResult, TResult, bool> ignore)
		{
			_ignores.Add(ignore);
			return this;
		}

		public ExperimentConfig<TResult> Context(Func<Dictionary<object, object>> createContext)
		{
			_createContext = createContext;
			return this;
		}

		public ExperimentConfig<TResult> Parallel()
		{
			_parallel = true;
			return this;
		}

		public ExperimentConfig<TResult> Publish(IPublisher publisher)
		{
			return Publish(publisher.Publish);
		}

		public ExperimentConfig<TResult> Publish(Action<Results> publish)
		{
			_publish = publish;
			return this;
		}

		public ExperimentConfig<TResult> Called(string name)
		{
			_control.TrialName = name;
			return this;
		}

		public ExperimentConfig<TResult> Clean<TCleaned>(Func<TResult, TCleaned> cleaner)
		{
			_cleaner = results => cleaner(results);
			return this;
		}

		public ExperimentConfig<TResult> ThrowMismatches()
		{
			_throwMismatches = true;
			return this;
		}

		public TResult Run()
		{
			var enabled = _isEnabled();

			if (enabled == false)
				return _control.Execute();

			var results = new Results
			{
				Name = _control.TrialName,
				Context = _createContext(),
				ExperimentEnabled = true
			};

			var actions = new List<Action>();

			actions.Add(() => results.Control = _control.Run(_cleaner));
			actions.AddRange(_tests.Select(test => new Action(() => results.AddObservation(test.Run(_cleaner)))));
			actions.Shuffle();

			if (_parallel)
				actions.AsParallel().ForAll(action => action());
			else
				actions.ForEach(action => action());

			var controlResult = results.Control.Result != null
				? (TResult)results.Control.Result
				: default(TResult);

			foreach (var trial in results.Trials)
			{
				var trialResult = trial.Result != null
					? (TResult)trial.Result
					: default(TResult);

				trial.Ignored = _ignores.Any(check => check(controlResult, trialResult));

				if (trial.Ignored == false)
					trial.Matched = _compare(controlResult, trialResult);
			}

			_publish(results);

			if (_throwMismatches && results.Trials.Any(o => o.Matched == false))
				throw new MismatchException(results);

			if (results.Control.Exception != null)
				throw results.Control.Exception;

			return controlResult;
		}

		//private Observation Run(Trial<TResult> action)
		//{
		//	var dto = new Observation();
		//	var sw = new Stopwatch();

		//	try
		//	{
		//		sw.Start();
		//		var result = action.Value();
		//		sw.Stop();

		//		dto.Name = action.Key;
		//		dto.Result = result;
		//		dto.CleanedResult = _cleaner(result);
		//	}
		//	catch (Exception ex)
		//	{
		//		sw.Stop();
		//		dto.Exception = ex;
		//	}
		//	finally
		//	{
		//		dto.Duration = sw.Elapsed;
		//	}

		//	return dto;
		//}
	}
}
