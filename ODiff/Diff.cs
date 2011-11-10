﻿using System.Collections;
using ODiff.Extensions;

namespace ODiff
{
    public class Diff
    {
        public static DiffReport ObjectValues(object left, object right)
        {
            return CompareObjectValues("obj", left, right);
        }

        private static DiffReport CompareObjectValues(string member, object left, object right)
        {
            if (left == null && right == null) return NoDiffFound();
            if ((left == null && right != null) ||
                (left != null && right == null)) 
                return new DiffReport(diffFound: true);

            var report = new DiffReport();

            if (left.IsList() &&
                right.IsList())
            {
                report.Merge(CompareLists(member, left as IList, right as IList));
            }

            report.Merge(CheckPublicFields(member, left, right));
            report.Merge(CheckGetterProperties(member, left, right));
            return report;
        }


        private static DiffReport CompareLists(string member, IList left, IList right)
        {
            var report = new DiffReport();
            for (int i = 0; i < left.Count; i++)
            {
                var leftValue = left[i];
                var rightValue = right[i];
                if (leftValue != null &&
                    leftValue.IsPrimitiveValueOrString())
                {
                    if (!AreEqual(leftValue, rightValue))
                    {
                        var listItemReport = new DiffReport(diffFound: true);
                        listItemReport.ReportDiff(member + "[" + i + "]", left[i], left[i]);
                        report.Merge(listItemReport);
                    }
                }
                else
                {
                    report.Merge(CompareObjectValues(member + "[" + i + "]", leftValue, rightValue));
                }
            }
            return report;
        }

        private static DiffReport NoDiffFound()
        {
            return new DiffReport(diffFound: false);
        }

        private static DiffReport CheckPublicFields(string member, object left, object right)
        {
            var diffReport = new DiffReport();
            var leftFields = left.PublicFields();
            var rightFields = right.PublicFields();

            for (int i = 0; i < leftFields.Length; i++)
            {
                var leftValue = leftFields[i].GetValue(left);
                var rightValue = rightFields[i].GetValue(right);

                if (!AreEqual(leftValue, rightValue))
                {
                    var fieldReport = new DiffReport(diffFound: true);
                    fieldReport.ReportDiff(member + "." + leftFields[i].Name, leftValue, rightValue);
                    diffReport.Merge(fieldReport);
                }
            }
            return diffReport;
        }

        private static bool AreEqual(object leftValue, object rightValue)
        {
            if (leftValue == null && rightValue == null) return true;
            if (leftValue == null && rightValue != null) return false;
            if (leftValue != null && rightValue == null) return false;

            if (leftValue.GetType() == typeof(int) &&
                rightValue.GetType() == typeof(int))
            {
                var leftAsInt = (int) leftValue;
                var rightAsInt = (int) rightValue;
                return leftAsInt == rightAsInt;
            }
            if (leftValue.GetType() == typeof(string) &&
                rightValue.GetType() == typeof(string))
                return leftValue.Equals(rightValue);
            if (leftValue.GetType().IsPrimitive)
                return leftValue.Equals(rightValue);
            if (leftValue.GetType() == rightValue.GetType())
                return true;

            return true; 
        }

        private static DiffReport CheckGetterProperties(string member, object left, object right)
        {
            var diffReport = new DiffReport();
            var leftGetterProps = left.PublicGetterProperties();
            var rightGetterProps = right.PublicGetterProperties();

            for (int i = 0; i < leftGetterProps.Length; i++)
            {
                var leftProperty = leftGetterProps[i];
                var rightProperty = rightGetterProps[i];

                if (!leftProperty.IsIndexerProperty() &&
                    !rightProperty.IsIndexerProperty())
                {
                    var leftValue = leftProperty.GetValue(left);
                    var rightValue = rightProperty.GetValue(right);

                    if (!AreEqual(leftValue, rightValue))
                    {
                        var propertyReport = new DiffReport(diffFound: true);
                        propertyReport.ReportDiff(member + "." + leftGetterProps[i].Name, leftValue, rightValue);
                        diffReport.Merge(propertyReport);
                    }
                }
            }
            return diffReport;
        }
    }
}
