using System;
using System.Collections.Generic;

namespace RevitMCP.Shared.Units
{
    public static class RevitUnitConverter
    {
        // 1 Foot = 304.8 Millimeters (exact)
        public const double FeetToMmRatio = 304.8;
        public const double MmToFeetRatio = 1.0 / FeetToMmRatio;

        // 1 Foot = 0.3048 Meters (exact)
        public const double FeetToMetersRatio = 0.3048;
        public const double MetersToFeetRatio = 1.0 / FeetToMetersRatio;

        // 1 Square Foot = 0.09290304 Square Meters (exact)
        public const double SqFeetToSqMetersRatio = 0.09290304;
        public const double SqMetersToSqFeetRatio = 1.0 / SqFeetToSqMetersRatio;

        // 1 Square Foot = 92903.04 Square Millimeters
        public const double SqFeetToSqMmRatio = 92903.04;
        public const double SqMmToSqFeetRatio = 1.0 / SqFeetToSqMmRatio;

        // 1 Cubic Foot = 0.028316846592 Cubic Meters (exact)
        public const double CuFeetToCuMetersRatio = 0.028316846592;
        public const double CuMetersToCuFeetRatio = 1.0 / CuFeetToCuMetersRatio;

        // Radians <-> Degrees
        public const double RadiansToDegreesRatio = 180.0 / Math.PI;
        public const double DegreesToRadiansRatio = Math.PI / 180.0;

        public static double MmToFeet(double mm) => mm * MmToFeetRatio;
        public static double FeetToMm(double feet) => feet * FeetToMmRatio;
        public static double DegreesToRadians(double deg) => deg * DegreesToRadiansRatio;
        public static double RadiansToDegrees(double rad) => rad * RadiansToDegreesRatio;
        public static double SqFeetToSqMeters(double sqFeet) => sqFeet * SqFeetToSqMetersRatio;
        public static double CuFeetToCuMeters(double cuFeet) => cuFeet * CuFeetToCuMetersRatio;

        /// <summary>
        /// Converts a value from user specified unit (e.g. "mm", "m", "m2", "degrees") into internal Revit units (feet, sq feet, radians).
        /// </summary>
        public static double ConvertToInternal(double value, string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit)) return value;

            switch (unit!.Trim().ToLowerInvariant())
            {
                case "mm":
                case "millimeter":
                case "millimeters":
                case "миллиметр":
                case "мм":
                    return value * MmToFeetRatio;

                case "m":
                case "meter":
                case "meters":
                case "метр":
                case "м":
                    return value * MetersToFeetRatio;

                case "cm":
                case "centimeter":
                case "centimeters":
                case "сантиметр":
                case "см":
                    return (value * 10.0) * MmToFeetRatio;

                case "in":
                case "inch":
                case "inches":
                case "дюйм":
                    return value / 12.0;

                case "ft":
                case "foot":
                case "feet":
                case "фут":
                    return value;

                case "m2":
                case "sqm":
                case "square_meter":
                case "кв_м":
                case "м2":
                    return value * SqMetersToSqFeetRatio;

                case "mm2":
                case "sqmm":
                case "мм2":
                    return value * SqMmToSqFeetRatio;

                case "m3":
                case "cum":
                case "cubic_meter":
                case "куб_м":
                case "м3":
                    return value * CuMetersToCuFeetRatio;

                case "deg":
                case "degree":
                case "degrees":
                case "градус":
                case "град":
                    return value * DegreesToRadiansRatio;

                case "rad":
                case "radian":
                case "radians":
                case "радиан":
                    return value;

                default:
                    return value;
            }
        }

        /// <summary>
        /// Converts an internal Revit value (feet, sq feet, radians) to a specified user unit.
        /// </summary>
        public static double ConvertFromInternal(double internalValue, string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit)) return internalValue;

            switch (unit!.Trim().ToLowerInvariant())
            {
                case "mm":
                case "millimeter":
                case "millimeters":
                case "миллиметр":
                case "мм":
                    return internalValue * FeetToMmRatio;

                case "m":
                case "meter":
                case "meters":
                case "метр":
                case "м":
                    return internalValue * FeetToMetersRatio;

                case "cm":
                case "centimeter":
                case "centimeters":
                case "сантиметр":
                case "см":
                    return (internalValue * FeetToMmRatio) / 10.0;

                case "in":
                case "inch":
                case "inches":
                case "дюйм":
                    return internalValue * 12.0;

                case "ft":
                case "foot":
                case "feet":
                case "фут":
                    return internalValue;

                case "m2":
                case "sqm":
                case "square_meter":
                case "кв_м":
                case "м2":
                    return internalValue * SqFeetToSqMetersRatio;

                case "mm2":
                case "sqmm":
                case "мм2":
                    return internalValue * SqFeetToSqMmRatio;

                case "m3":
                case "cum":
                case "cubic_meter":
                case "куб_м":
                case "м3":
                    return internalValue * CuFeetToCuMetersRatio;

                case "deg":
                case "degree":
                case "degrees":
                case "градус":
                case "град":
                    return internalValue * RadiansToDegreesRatio;

                case "rad":
                case "radian":
                case "radians":
                case "радиан":
                    return internalValue;

                default:
                    return internalValue;
            }
        }
    }
}
