﻿//  Copyright 2016 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.

using NtApiDotNet;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace NtObjectManager
{
    /// <summary>
    /// <para type="synopsis">Get NT threads.</para>
    /// <para type="description">This cmdlet gets all accessible threads on the system. You can specify a specific thread by setting the -ThreadId parameter.</para>
    /// <para>Note that thread objects need to be disposed of after use, therefore capture them in a Dispose List or manually Close them once used.</para>
    /// </summary>
    /// <example>
    ///   <code>$ts = Get-NtThread</code>
    ///   <para>Get all NT threads accessible by the current user.</para>
    /// </example>
    /// <example>
    ///   <code>$ts = Get-NtThread -Access Impersonate</code>
    ///   <para>Get all NT threads accessible by the current user for impersonate access.</para>
    /// </example>
    /// <example>
    ///   <code>$t = Get-NtThread 1234</code>
    ///   <para>Get a specific thread.</para>
    /// </example>
    /// <example>
    ///   <code>$t = Get-NtThread -ProcessId 1234</code>
    ///   <para>Get threads for a specific process.</para>
    /// </example>
    /// <example>
    ///   <code>$ts = Get-NtThread -Current</code>
    ///   <para>Get the current NT thread.</para>
    /// </example>
    /// <example>
    ///   <code>$ts = Get-NtThread -FilterScript { Use-NtObject($k = $_.OpenToken()) { $k -ne $null } }</code>
    ///   <para>Get threads which have impersonation tokens set.</para>
    /// </example>
    /// <example>
    ///   <code>Get-NtThread -InfoOnly</code>
    ///   <para>Get all thread information, but don't open the thread objects.</para>
    /// </example>
    /// <example>
    ///   <code>Get-NtThread -InfoOnly -ProcessId $pid</code>
    ///   <para>Get all thread information for the current process, but don't open the thread objects.</para>
    /// </example>
    /// <para type="link">about_ManagingNtObjectLifetime</para>
    [Cmdlet(VerbsCommon.Get, "NtThread", DefaultParameterSetName = "all")]
    [OutputType(typeof(NtThread))]
    public class GetNtThreadCmdlet : Cmdlet
    {
        /// <summary>
        /// <para type="description">Specify a thread ID to open.</para>
        /// </summary>
        [Parameter(Position = 0, ParameterSetName = "tid", Mandatory = true)]
        [Alias(new string[] { "tid" })]
        public int ThreadId { get; set; }

        /// <summary>
        /// <para type="description">Specify a process ID to enumerate only its threads.</para>
        /// </summary>
        [Parameter(ParameterSetName = "pid", Mandatory = true), Parameter(ParameterSetName = "infoonly")]
        [Alias(new string[] { "pid" })]
        public int ProcessId { get; set; }

        /// <summary>
        /// <para type="description">Get the current thread.</para>
        /// </summary>
        [Parameter(ParameterSetName = "current")]
        public SwitchParameter Current { get; set; }

        /// <summary>
        /// <para type="description">When getting the current thread return pseudo handle. 
        /// This handle doesn't need to be closed but changes identity if used in a different thread.</para>
        /// </summary>
        [Parameter(ParameterSetName = "current")]
        public SwitchParameter PseudoHandle { get; set; }

        /// <summary>
        /// <para type="description">Specify an arbitrary filter script.</para>
        /// </summary>
        [Parameter(ParameterSetName = "all")]
        public ScriptBlock FilterScript { get; set; }

        /// <summary>
        /// <para type="description">Specify access rights for each thread opened.</para>
        /// </summary>
        [Parameter(ParameterSetName = "all"), Parameter(ParameterSetName = "tid"), Parameter(ParameterSetName = "pid"), Parameter(ParameterSetName = "current")]
        public ThreadAccessRights Access { get; set; }

        /// <summary>
        /// <para type="description">When getting all threads only get the system information thread list.</para>
        /// </summary>
        [Parameter(ParameterSetName = "all")]
        public SwitchParameter FromSystem { get; set; }

        /// <summary>
        /// <para type="description">Only get thread information, do not open the objects.</para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "infoonly")]
        public SwitchParameter InfoOnly { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public GetNtThreadCmdlet()
        {
            Access = ThreadAccessRights.MaximumAllowed;
            ThreadId = -1;
            ProcessId = -1;
        }

        private static bool ArbitraryFilter(NtThread thread, ScriptBlock filter)
        {
            return filter.InvokeWithArg(false, thread);
        }

        private static NtThread GetCurrentThread(ThreadAccessRights access, bool pseudo_handle)
        {
            if (pseudo_handle)
            {
                return NtThread.Current;
            }
            else if ((access & ThreadAccessRights.MaximumAllowed) == ThreadAccessRights.MaximumAllowed)
            {
                return NtThread.Current.Duplicate();
            }

            return NtThread.Current.Duplicate(access);
        }

        /// <summary>
        /// Overridden ProcessRecord method.
        /// </summary>
        protected override void ProcessRecord()
        {
            if (InfoOnly)
            {
                if (ProcessId != -1)
                {
                    WriteObject(NtSystemInfo.GetThreadInformation(ProcessId), true);
                }
                else
                {
                    WriteObject(NtSystemInfo.GetThreadInformation(), true);
                }
            }
            else if (Current)
            {
                WriteObject(GetCurrentThread(Access, PseudoHandle));
            }
            else if (ThreadId == -1 && ProcessId == -1)
            {
                IEnumerable<NtThread> threads = NtThread.GetThreads(Access, FromSystem);
                if (FilterScript == null)
                {
                    WriteObject(threads, true);
                }
                else
                {
                    using (var ths = new DisposableList<NtThread>(threads))
                    {
                        WriteObject(ths.Where(t => ArbitraryFilter(t, FilterScript)).Select(t => t.Duplicate()).ToArray(), true);
                    }
                }
            }
            else if (ProcessId != -1)
            {
                using (NtProcess process = NtProcess.Open(ProcessId, ProcessAccessRights.MaximumAllowed))
                {
                    WriteObject(process.GetThreads(), true);
                }
            }
            else
            {
                WriteObject(NtThread.Open(ThreadId, Access));
            }
        }
    }
}
