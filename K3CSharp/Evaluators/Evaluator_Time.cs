// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;

namespace K3CSharp
{
    public partial class Evaluator
    {
        // Time-related functions
        
        public K3Value TimeFunction(K3Value operand)
        {
            // _t is a niladic getter that returns an integer value representing current K-time
            // K-time is defined as seconds since the base timedate 12:00 AM, January 1, 2035 UTC
            // For times earlier than base timedate, K-time is a negative value.
            
            var baseTime = new DateTime(2035, 1, 1);
            var currentTime = DateTime.UtcNow;
            var kTime = (long)(currentTime - baseTime).TotalSeconds;
            
            return new IntegerValue((int)kTime);
        }

        public K3Value DirectoryFunction(K3Value operand)
        {
            // _d is a niladic getter that returns the current K tree branch name
            return kTree.CurrentBranch ?? new SymbolValue("");
        }

        private K3Value TFunction(K3Value operand)
        {
            // _T is a niladic getter that returns a float value representing current time in Days since the base timedate 12:00 AM, January 1, 2035
            // For times earlier than the base timedate it returns a negative value.
            // Using _T allows for using as much precision as C# DateTime allows, better than _t, which is limited to 1-second precision.
            
            var baseTime = new DateTime(2035, 1, 1);
            var currentTime = DateTime.UtcNow;
            var daysSinceBase = (currentTime - baseTime).TotalDays;
            
            return new FloatValue(daysSinceBase);
        }

        private K3Value LtFunction(K3Value operand)
        {
            // _lt is a monadic operator that adds a GMT-to-local-time offset in seconds to a K-time value
            // E.g. if local time zone is US Eastern Standard Time (UTC-5), the offset is -18000 seconds and _lt will return the input value plus -18000
            
            if (operand is IntegerValue intVal)
            {
                var kTime = intVal.Value;
                var adjustedTime = kTime - 18000; // Assuming US Eastern Standard Time (UTC-5)
                return new IntegerValue(adjustedTime);
            }
            
            throw new Exception("_lt requires integer argument representing K-time");
        }

        private K3Value GtimeFunction(K3Value operand)
        {
            // _gtime is a monadic operator that converts an integer value representing a K-time to an integer vector of 2 integers
            // The first integer is result of 10000 * year + 100 * month + day
            // The second integer is result of 10000 * hour (in 24h format) + 100 * minute + second
            
            if (operand is IntegerValue intVal)
            {
                var kTime = intVal.Value;
                
                // Convert K-time to DateTime (base is January 1, 2035)
                var baseTime = new DateTime(2035, 1, 1);
                var targetTime = baseTime.AddSeconds(kTime);
                
                var year = targetTime.Year;
                var month = targetTime.Month;
                var day = targetTime.Day;
                var hour = targetTime.Hour;
                var minute = targetTime.Minute;
                var second = targetTime.Second;
                
                var firstInt = year * 10000 + month * 100 + day;
                var secondInt = hour * 10000 + minute * 100 + second;
                
                return new VectorValue(new List<K3Value> 
                { 
                    new IntegerValue(firstInt), 
                    new IntegerValue(secondInt) 
                });
            }
            
            throw new Exception("_gtime requires integer argument representing K-time");
        }

        private K3Value JdFunction(K3Value operand)
        {
            // _jd is a monadic operator that converts an integer value that represents the result of 10000 * year + 100 * month + day to a J-Date (K Julian Date)
            // A J-Date is defined as an integer value representing the number of days since the base date of January 1, 2035
            
            if (operand is IntegerValue intVal)
            {
                var dateInt = intVal.Value;
                
                // Extract year, month, day from the integer
                var year = dateInt / 10000;
                var month = (dateInt % 10000) / 100;
                var day = dateInt % 100;
                
                // Convert to DateTime to calculate J-Date
                var targetTime = new DateTime(year, month, day);
                var baseTime = new DateTime(2035, 1, 1);
                var jDate = (int)(targetTime - baseTime).TotalDays;
                
                // Special case: J-Date 0 should return null (0N)
                if (jDate == 0)
                {
                    return new NullValue();
                }
                
                return new IntegerValue(jDate);
            }
            
            throw new Exception("_jd requires integer argument representing year*10000 + month*100 + day");
        }

        private K3Value DjFunction(K3Value operand)
        {
            // _dj is a monadic operator that converts a J-Date to an integer value that is the result of 10000 * year + 100 * month + day
            
            if (operand is IntegerValue intVal)
            {
                var jDate = intVal.Value;
                
                // Convert J-Date to DateTime (base is January 1, 2035)
                var baseTime = new DateTime(2035, 1, 1);
                var targetTime = baseTime.AddDays(jDate);
                
                var year = targetTime.Year;
                var month = targetTime.Month;
                var day = targetTime.Day;
                
                var result = year * 10000 + month * 100 + day;
                
                return new IntegerValue(result);
            }
            
            throw new Exception("_dj requires integer argument representing J-Date");
        }

        private K3Value LtimeFunction(K3Value operand)
        {
            // _ltime is a monadic operator that converts an integer value representing a K-time to an integer vector of 2 integers that represents local time
            // It is equivalent to {[x] _gtime _lt x}
            
            if (operand is IntegerValue intVal)
            {
                var kTime = intVal.Value;
                
                // Apply timezone offset first (equivalent to _lt x)
                var adjustedKTime = kTime - 18000; // Assuming US Eastern Standard Time (UTC-5)
                
                // Convert adjusted K-time to DateTime (base is January 1, 2035)
                var baseTime = new DateTime(2035, 1, 1);
                var targetTime = baseTime.AddSeconds(adjustedKTime);
                
                var year = targetTime.Year;
                var month = targetTime.Month;
                var day = targetTime.Day;
                var hour = targetTime.Hour;
                var minute = targetTime.Minute;
                var second = targetTime.Second;
                
                var firstInt = year * 10000 + month * 100 + day;
                var secondInt = hour * 10000 + minute * 100 + second;
                
                return new VectorValue(new List<K3Value> 
                { 
                    new IntegerValue(firstInt), 
                    new IntegerValue(secondInt) 
                });
            }
            
            throw new Exception("_ltime requires integer argument representing K-time");
        }
    }
}