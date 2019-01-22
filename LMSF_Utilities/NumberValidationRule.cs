using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Text.RegularExpressions;

namespace LMSF_Utilities
{
    //code mostly copied from: https://docs.microsoft.com/en-us/dotnet/framework/wpf/app-development/dialog-boxes-overview
    class NumberValidationRule : ValidationRule
    {
        public double NumberMin { get; set; }
        public double NumberMax { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            double number;

            // Is it a number?
            if (!double.TryParse((string)value, out number))
            {
                return new ValidationResult(false, "Not a number.");
            }

            // Is in range?
            if ((number < NumberMin) || (number > NumberMax))
            {
                string msg = string.Format("Number must be between {0} and {1}.", NumberMin, NumberMax);
                return new ValidationResult(false, msg);
            }

            // Number is valid
            return new ValidationResult(true, null);
        }
    }
}
