using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class InitializeResponse : ResponseBody
	{
		[JsonProperty("supportsConfigurationDoneRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsConfigurationDoneRequest
		{
			get;
			set;
		}

		[JsonProperty("supportsFunctionBreakpoints", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsFunctionBreakpoints
		{
			get;
			set;
		}

		[JsonProperty("supportsConditionalBreakpoints", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsConditionalBreakpoints
		{
			get;
			set;
		}

		[JsonProperty("supportsHitConditionalBreakpoints", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsHitConditionalBreakpoints
		{
			get;
			set;
		}

		[JsonProperty("supportsEvaluateForHovers", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsEvaluateForHovers
		{
			get;
			set;
		}

		[JsonProperty("exceptionBreakpointFilters", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public List<ExceptionBreakpointsFilter> ExceptionBreakpointFilters
		{
			get;
			set;
		}

		[JsonProperty("supportsStepBack", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsStepBack
		{
			get;
			set;
		}

		[JsonProperty("supportsSetVariable", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsSetVariable
		{
			get;
			set;
		}

		[JsonProperty("supportsRestartFrame", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsRestartFrame
		{
			get;
			set;
		}

		[JsonProperty("supportsGotoTargetsRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsGotoTargetsRequest
		{
			get;
			set;
		}

		[JsonProperty("supportsStepInTargetsRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsStepInTargetsRequest
		{
			get;
			set;
		}

		[JsonProperty("supportsCompletionsRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsCompletionsRequest
		{
			get;
			set;
		}

		[JsonProperty("supportsModulesRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsModulesRequest
		{
			get;
			set;
		}

		[JsonProperty("additionalModuleColumns", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public List<ColumnDescriptor> AdditionalModuleColumns
		{
			get;
			set;
		}

		[JsonProperty("supportedChecksumAlgorithms", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public List<ChecksumAlgorithm> SupportedChecksumAlgorithms
		{
			get;
			set;
		}

		[JsonProperty("supportsRestartRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsRestartRequest
		{
			get;
			set;
		}

		[JsonProperty("supportsExceptionOptions", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsExceptionOptions
		{
			get;
			set;
		}

		[JsonProperty("supportsValueFormattingOptions", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsValueFormattingOptions
		{
			get;
			set;
		}

		[JsonProperty("supportsExceptionInfoRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsExceptionInfoRequest
		{
			get;
			set;
		}

		[JsonProperty("supportTerminateDebuggee", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportTerminateDebuggee
		{
			get;
			set;
		}

		[JsonProperty("supportsDelayedStackTraceLoading", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsDelayedStackTraceLoading
		{
			get;
			set;
		}

		[JsonProperty("supportsLoadedSourcesRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsLoadedSourcesRequest
		{
			get;
			set;
		}

		[JsonProperty("supportsLogPoints", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsLogPoints
		{
			get;
			set;
		}

		[JsonProperty("supportsTerminateThreadsRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsTerminateThreadsRequest
		{
			get;
			set;
		}

		[JsonProperty("supportsSetExpression", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsSetExpression
		{
			get;
			set;
		}

		[JsonProperty("supportsTerminateRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsTerminateRequest
		{
			get;
			set;
		}

		[JsonProperty("supportsDataBreakpoints", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsDataBreakpoints
		{
			get;
			set;
		}

		[JsonProperty("supportsReadMemoryRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsReadMemoryRequest
		{
			get;
			set;
		}

		[JsonProperty("supportsDisassembleRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsDisassembleRequest
		{
			get;
			set;
		}

		[JsonProperty("supportsExceptionConditions", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsExceptionConditions
		{
			get;
			set;
		}

		[JsonProperty("supportsLoadSymbolsRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsLoadSymbolsRequest
		{
			get;
			set;
		}

		[JsonProperty("supportsModuleSymbolSearchLog", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsModuleSymbolSearchLog
		{
			get;
			set;
		}

		[JsonProperty("supportsDebuggerProperties", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsDebuggerProperties
		{
			get;
			set;
		}

		[JsonProperty("supportsSetSymbolOptions", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? SupportsSetSymbolOptions
		{
			get;
			set;
		}

		[JsonExtensionData(ReadData = true, WriteData = true)]
		public new Dictionary<string, JToken> AdditionalProperties
		{
			get
			{
				return base.AdditionalProperties;
			}
			set
			{
				base.AdditionalProperties = value;
			}
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public InitializeResponse(bool? supportsConfigurationDoneRequest = null, bool? supportsFunctionBreakpoints = null, bool? supportsConditionalBreakpoints = null, bool? supportsHitConditionalBreakpoints = null, bool? supportsEvaluateForHovers = null, List<ExceptionBreakpointsFilter> exceptionBreakpointFilters = null, bool? supportsStepBack = null, bool? supportsSetVariable = null, bool? supportsRestartFrame = null, bool? supportsGotoTargetsRequest = null, bool? supportsStepInTargetsRequest = null, bool? supportsCompletionsRequest = null, bool? supportsModulesRequest = null, List<ColumnDescriptor> additionalModuleColumns = null, List<ChecksumAlgorithm> supportedChecksumAlgorithms = null, bool? supportsRestartRequest = null, bool? supportsExceptionOptions = null, bool? supportsValueFormattingOptions = null, bool? supportsExceptionInfoRequest = null, bool? supportTerminateDebuggee = null, bool? supportsDelayedStackTraceLoading = null, bool? supportsLoadedSourcesRequest = null, bool? supportsExceptionConditions = null, bool? supportsDebuggerProperties = null, bool? supportsSetExpression = null)
		{
			SupportsConfigurationDoneRequest = supportsConfigurationDoneRequest;
			SupportsFunctionBreakpoints = supportsFunctionBreakpoints;
			SupportsConditionalBreakpoints = supportsConditionalBreakpoints;
			SupportsHitConditionalBreakpoints = supportsHitConditionalBreakpoints;
			SupportsEvaluateForHovers = supportsEvaluateForHovers;
			ExceptionBreakpointFilters = exceptionBreakpointFilters;
			SupportsStepBack = supportsStepBack;
			SupportsSetVariable = supportsSetVariable;
			SupportsRestartFrame = supportsRestartFrame;
			SupportsGotoTargetsRequest = supportsGotoTargetsRequest;
			SupportsStepInTargetsRequest = supportsStepInTargetsRequest;
			SupportsCompletionsRequest = supportsCompletionsRequest;
			SupportsModulesRequest = supportsModulesRequest;
			AdditionalModuleColumns = additionalModuleColumns;
			SupportedChecksumAlgorithms = supportedChecksumAlgorithms;
			SupportsRestartRequest = supportsRestartRequest;
			SupportsExceptionOptions = supportsExceptionOptions;
			SupportsValueFormattingOptions = supportsValueFormattingOptions;
			SupportsExceptionInfoRequest = supportsExceptionInfoRequest;
			SupportTerminateDebuggee = supportTerminateDebuggee;
			SupportsDelayedStackTraceLoading = supportsDelayedStackTraceLoading;
			SupportsLoadedSourcesRequest = supportsLoadedSourcesRequest;
			SupportsExceptionConditions = supportsExceptionConditions;
			SupportsDebuggerProperties = supportsDebuggerProperties;
			SupportsSetExpression = supportsSetExpression;
		}

		public InitializeResponse()
		{
			ExceptionBreakpointFilters = new List<ExceptionBreakpointsFilter>();
			AdditionalModuleColumns = new List<ColumnDescriptor>();
			SupportedChecksumAlgorithms = new List<ChecksumAlgorithm>();
		}
	}
}
