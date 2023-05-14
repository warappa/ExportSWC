using System;
using System.Diagnostics.CodeAnalysis;
using ExportSWC.Options;
using System.Runtime.CompilerServices;

namespace ExportSWC.Utils
{
    internal static class Guards
    {
        public static SWCProject EnsureNotNull([NotNull] SWCProject? swcProject)
        {
            return EnsureNotNull(swcProject, "No current SWC project found");
        }

        public static T EnsureNotNull<T>([NotNull] T? obj, [CallerArgumentExpression(nameof(obj))] string? message = null)
        {
            if (obj is null)
            {
                throw new InvalidOperationException($"{message} is null");
            }

            return obj;
        }
    }
}
