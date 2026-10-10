using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CrazyGames
{
    // Unity asset APIs stay on the main thread; yield between small units of work.
    internal sealed class AnalysisJob
    {
        private IEnumerator<string> _steps;
        private int _skipped;
        private string _message;
        private MessageType _messageType;
        private readonly string _completedMessage;
        private readonly string _cancelledMessage;
        private readonly string _failureMessage;
        public bool IsRunning => _steps != null;

        public AnalysisJob(string completedMessage = "Analysis complete.",
            string cancelledMessage = "Analysis cancelled. Previous results retained.",
            string failureMessage = "Previous results retained.")
        {
            _completedMessage = completedMessage;
            _cancelledMessage = cancelledMessage;
            _failureMessage = failureMessage;
        }

        public void Start(Func<IEnumerator<string>> createSteps)
        {
            if (IsRunning)
                return;
            _skipped = 0;
            _message = "Starting analysis...";
            _messageType = MessageType.Info;
            try
            {
                _steps = createSteps();
                if (_steps == null)
                    throw new InvalidOperationException("Analysis did not provide any work.");
                EditorApplication.update += Update;
                AssemblyReloadEvents.beforeAssemblyReload += Cancel;
                EditorApplication.quitting += Cancel;
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
            Repaint();
        }

        private void Update()
        {
            try
            {
                double deadline = EditorApplication.timeSinceStartup + 0.008;
                int count = 0;
                do
                {
                    if (!_steps.MoveNext())
                    {
                        _message = _skipped == 0 ? _completedMessage :
                            $"{_completedMessage} Skipped {_skipped} item(s); see the Console for paths and reasons.";
                        _messageType = _skipped == 0 ? MessageType.Info : MessageType.Warning;
                        Stop();
                        break;
                    }
                    _message = _steps.Current;
                }
                while (++count < 100 && EditorApplication.timeSinceStartup < deadline);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
            Repaint();
        }

        public void ReportSkipped(string path, Exception exception)
        {
            _skipped++;
            Debug.LogWarning($"WebGL Optimizer skipped '{path}': {exception.Message}");
        }

        public void Cancel()
        {
            if (!IsRunning)
                return;
            _message = _cancelledMessage;
            _messageType = MessageType.Info;
            try { Stop(); }
            catch (Exception exception) { Fail(exception); }
            Repaint();
        }

        private void Fail(Exception exception)
        {
            _message = $"Operation failed: {exception.Message}. {_failureMessage}";
            _messageType = MessageType.Error;
            Debug.LogException(exception);
            try { Stop(); }
            catch (Exception cleanupException) { Debug.LogException(cleanupException); }
        }

        private void Stop()
        {
            // Clear running state even if disposing a file reader fails.
            var steps = _steps;
            try { steps?.Dispose(); }
            finally
            {
                _steps = null;
                EditorApplication.update -= Update;
                AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
                EditorApplication.quitting -= Cancel;
            }
        }

        public void RenderStatus()
        {
            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, _messageType);
        }

        private static void Repaint()
        {
            if (OptimizerWindow.EditorWindowInstance != null)
                OptimizerWindow.EditorWindowInstance.Repaint();
        }
    }
}
