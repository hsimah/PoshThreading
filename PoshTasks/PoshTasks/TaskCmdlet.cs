﻿using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PoshTasks.Cmdlets
{
    public abstract class TaskCmdlet<TIn, TOut> : Cmdlet
        where TIn : class
        where TOut : class
    {
        /// <summary>
        /// Gets or sets the error collection
        /// </summary>
        protected List<ErrorRecord> Errors { get; private set; }

        /// <summary>
        /// Gets or sets the flag whether to write errors
        /// </summary>
        protected virtual bool WriteErrors { get; private set; }

        /// <summary>
        /// Gets or sets the input object collection
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = "InputObject")]
        public virtual TIn[] InputObject { get; set; }

        /// <summary>
        /// Performs an action on <paramref name="server"/>
        /// </summary>
        /// <param name="input">The <see cref="object"/> to be processed; null if not processing input</param>
        /// <returns>A <see cref="T"/></returns>
        protected abstract TOut ProcessTask(TIn input = null);

        /// <summary>
        /// Initialises a new instance of the TaskCmdlet class
        /// </summary>
        public TaskCmdlet()
        {
            Errors = new List<ErrorRecord>();
            WriteErrors = true;
        }

        /// <summary>
        /// Creates a collection of tasks to be processed
        /// </summary>
        /// <returns>A collection of tasks</returns>
        [Obsolete]
        protected virtual IEnumerable<Task<TOut>> GenerateTasks()
        {
            return CreateProcessTasks();
        }

        /// <summary>
        /// Creates a collection of tasks to be processed
        /// </summary>
        /// <returns>A collection of tasks</returns>
        protected virtual IEnumerable<Task<TOut>> CreateProcessTasks()
        {
            if (InputObject == null)
            {
                yield return Task.Run(() => ProcessTask());
                yield break;
            }

            foreach (var input in InputObject)
            {
                yield return Task.Run(() => ProcessTask(input));
            }
        }

        /// <summary>
        /// Performs the pipeline output for this cmdlet
        /// </summary>
        /// <param name="result"></param>
        protected virtual void PostProcessTask(TOut result)
        {
            WriteObject(result, true);
        }

        /// <summary>
        /// Processes cmdlet operation
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                var tasks = CreateProcessTasks();
                var results = Task.WhenAll(tasks);

                results.Wait();

                // Write results
                foreach (var result in results.Result)
                {
                    PostProcessTask(result);
                }

                if (WriteErrors)
                {
                    // Write errors
                    foreach (var error in Errors)
                    {
                        WriteError(error);
                    }
                }
            }
            catch (Exception e) when (e is PipelineStoppedException || e is PipelineClosedException)
            {
                // do nothing if pipeline stops
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.Flatten().InnerExceptions)
                {
                    WriteError(new ErrorRecord(e, e.GetType().Name, ErrorCategory.NotSpecified, this));
                }
            }
            catch (Exception e)
            {
                WriteError(new ErrorRecord(e, e.GetType().Name, ErrorCategory.NotSpecified, this));
            }
        }
    }
}