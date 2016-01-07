using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NScientist
{
	public class ExperimentConfig<TResult>
	{
		private readonly Func<TResult> _control;
		private Func<TResult> _test;
		private Func<bool> _isEnabled;
		private Action<Results> _publish;
		private Func<TResult, TResult, bool> _compare;
		private Func<Dictionary<object, object>> _createContext;
		private string _name;
		private Func<TResult, object> _cleaner;
		private List<Func<TResult, TResult, bool>> _ignores;

		public ExperimentConfig(Func<TResult> action)
		{
			_control = action;
			_isEnabled = () => true;
			_publish = results => { };
			_compare = (control, experiment) => Equals(control, experiment);
			_createContext = () => new Dictionary<object, object>();
			_name = "Unnamed Experiment";
			_cleaner = results => null;
			_ignores = new List<Func<TResult, TResult, bool>>();
		}

		public ExperimentConfig<TResult> Try(Func<TResult> action)
		{
			_test = action;
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

		public ExperimentConfig<TResult> Publish(Action<Results> publish)
		{
			_publish = publish;
			return this;
		}

		public ExperimentConfig<TResult> Called(string name)
		{
			_name = name;
			return this;
		}

		public ExperimentConfig<TResult> Clean<TCleaned>(Func<TResult, TCleaned> cleaner)
		{
			_cleaner = results => cleaner(results);
			return this;
		}

		public TResult Run()
		{
			var results = new Results
			{
				Name = _name,
				Context = _createContext(),
				ExperimentEnabled = _isEnabled()
			};

			var controlResult = default(TResult);

			var actions = new List<Action>();

			actions.Add(() =>
			{
				var control = Run(_control);

				results.ControlException = control.Exception;
				results.ControlDuration = control.Duration;
				results.ControlResult = control.Result;
				results.ControlCleanedResult = _cleaner(control.Result);

				controlResult = control.Result;
			});

			if (results.ExperimentEnabled)
			{
				results.ExperimentEnabled = true;

				actions.Add(() =>
				{
					var experiment = Run(_test);

					results.ExperimentException = experiment.Exception;
					results.ExperimentDuration = experiment.Duration;
					results.ExperimentResult = experiment.Result;
					results.ExperimentCleanedResult = _cleaner(experiment.Result);
				});
			}

			actions.Shuffle();
			actions.ForEach(action => action());

			if (results.ExperimentEnabled)
			{
				if (_ignores.Any(check => check((TResult)results.ControlResult, (TResult)results.ExperimentResult)) == false)
					results.Matched = _compare((TResult)results.ControlResult, (TResult)results.ExperimentResult);

				_publish(results);
			}

			if (results.ControlException != null)
				throw results.ControlException;

			return controlResult;
		}

		private class RunDto
		{
			public TimeSpan Duration;
			public Exception Exception;
			public TResult Result;
		}

		private RunDto Run(Func<TResult> action)
		{
			var dto = new RunDto();
			var sw = new Stopwatch();

			try
			{
				sw.Start();
				dto.Result = action();
				sw.Stop();
			}
			catch (Exception ex)
			{
				sw.Stop();
				dto.Exception = ex;
			}
			finally
			{
				dto.Duration = sw.Elapsed;
			}

			return dto;
		}
	}
}
