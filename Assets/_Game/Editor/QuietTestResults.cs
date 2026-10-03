using System;
using System.Linq;
using UnityEditor;

namespace Vespertine.EditorTools
{
    /// <summary>
    /// The performance-testing package (pulled in as a dependency) registers a callback that writes TestResults.xml into
    /// Application.persistentDataPath after every editor test run: the same folder as real player saves and settings.
    /// This removes that callback after each domain reload, so running tests leaves nothing outside the project.
    /// Results still come back through the runner itself.
    /// </summary>
    [InitializeOnLoad]
    public static class QuietTestResults
    {
        static QuietTestResults()
        {
            // the package registers from its own [InitializeOnLoad]; run after every such initialiser has had its turn
            EditorApplication.delayCall += () => Strip();
        }

        /// <summary>Returns how many result savers were removed.</summary>
        public static int Strip()
        {
            try
            {
                var holderType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("UnityEditor.TestTools.TestRunner.Api.CallbacksHolder")).FirstOrDefault(t => t != null);
                if (holderType == null) return 0;
                var holder = holderType.BaseType?.GetProperty("instance")?.GetValue(null);
                if (holder == null) return 0;
                var all = (Array)holderType.GetMethod("GetAll").Invoke(holder, null);
                var remove = holderType.GetMethod("Remove");
                int n = 0;
                foreach (var cb in all)
                    if (cb != null && cb.GetType().FullName == "Unity.PerformanceTesting.Editor.PerformanceTestRunSaver")
                    {
                        remove.Invoke(holder, new[] { cb });
                        n++;
                    }
                return n;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[QuietTestResults] could not unhook the performance result saver: " + e.Message);
                return 0;
            }
        }
    }
}
