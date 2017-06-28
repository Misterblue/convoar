/*
 * Copyright (c) 2017 Robert Adams
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using log4net;

namespace org.herbal3d.convoar {

    public abstract class Logger {
        public abstract void SetVerbose(bool val);
        public abstract void Log(string msg, params Object[] args);
        public abstract void DebugFormat(string msg, params Object[] args);
        public abstract void ErrorFormat(string msg, params Object[] args);
    }

    public class LoggerConsole : Logger {
        private static readonly ILog _log = LogManager.GetLogger("convoar");

        private bool _verbose = false;
        public override void SetVerbose(bool value) {
            bool nextValue = value;
            if (!_verbose && nextValue) {
                // turning Verbose on
                LogManager.GetRepository().Threshold = log4net.Core.Level.Debug;
            }
            if (_verbose && !nextValue) {
                // turning Verbose off
                LogManager.GetRepository().Threshold = log4net.Core.Level.Info;
            }
            _verbose = nextValue;
        }

        public override void Log(string msg, params Object[] args) {
            System.Console.WriteLine(msg, args);
        }

        // Output the message if 'Verbose' is true
        public override void DebugFormat(string msg, params Object[] args) {
            if (_verbose) {
                System.Console.WriteLine(msg, args);
            }
        }

        public override void ErrorFormat(string msg, params Object[] args) {
            System.Console.WriteLine(msg, args);
        }
    }

    // Do logging with Log4net
    public class LoggerLog4Net : Logger {
        private static readonly ILog _log = LogManager.GetLogger("convoar");

        private bool _verbose = false;
        public override void SetVerbose(bool value) {
            bool nextValue = value;
            if (!_verbose && nextValue) {
                // turning Verbose on
                LogManager.GetRepository().Threshold = log4net.Core.Level.Debug;
            }
            if (_verbose && !nextValue) {
                // turning Verbose off
                LogManager.GetRepository().Threshold = log4net.Core.Level.Info;
            }
            _verbose = nextValue;
        }

        public override void Log(string msg, params Object[] args) {
            _log.InfoFormat(msg, args);
        }

        // Output the message if 'Verbose' is true
        public override void DebugFormat(string msg, params Object[] args) {
            _log.DebugFormat(msg, args);
        }

        public override void ErrorFormat(string msg, params Object[] args) {
            _log.ErrorFormat(msg, args);
        }
    }
}
