using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace NScientist
{
	public class ExperimentConfig<TResult>
	{
		private readonly Func<TResult> _control;
		private Func<TResult> _test;
		private Func<bool> _isEnabled;
		private Action<Results> _publish;

		public ExperimentConfig(Func<TResult> action)
		{
			_control = action;
			_isEnabled = () => true;
			_publish = results => { };
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

		public ExperimentConfig<TResult> Publish(Action<Results> publish)
		{
			_publish = publish;
			return this;
		}

		public TResult Run()
		{
			var results = new Results();
			var controlResult = default(TResult);

			var actions = new List<Action>();
			var experimentEnabled = _isEnabled();

            actions.Add(() =>
			{
				var control = Run(_control);

				results.ControlException = control.Exception;
				results.ControlDuration = control.Duration;
				results.ControlResult = control.Result;

				controlResult = control.Result;
			});
			
			if (experimentEnabled)
			{
				results.ExperimentEnabled = true;

				actions.Add(() =>
				{
					var experiment = Run(_test);

					results.TryException = experiment.Exception;
					results.TryDuration = experiment.Duration;
					results.TryResult = experiment.Result;
				});
			}
			
			actions.Shuffle();
			actions.ForEach(action => action());

			if (experimentEnabled)
				results.Matched = Equals(results.ControlResult, results.TryResult);

			_publish(results);

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
