﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ODiff.Extensions;
using ODiff.Interceptors;

namespace ODiff
{
    internal class ObjectGraphDiff
    {
        private readonly object leftRootNode;
        private readonly object rightRootNode;
        private readonly List<INodeInterceptor> nodeInterceptors = new List<INodeInterceptor>();
        private readonly DiffReport report;
        private readonly HashSet<Object> visitedNodes = new HashSet<Object>();

        public ObjectGraphDiff(Object leftRootNode, Object rightRootNode, params INodeInterceptor[] interceptors)
        {
            report = NoDiffFound();
            this.rightRootNode = rightRootNode;
            this.leftRootNode = leftRootNode;
            nodeInterceptors.AddRange(interceptors);
        }

        public DiffReport Diff()
        {
            VisitNode("", leftRootNode, rightRootNode);
            return report;
        }

        private void VisitNode(string memberPath, object leftNode, object rightNode)
        {
            leftNode = InterceptNode(memberPath, leftNode);
            rightNode = InterceptNode(memberPath, rightNode);

            if (IsALeafNode(leftNode) || IsALeafNode(rightNode))
            {
                CompareLeafNode(memberPath, leftNode, rightNode);
                return;
            }

            CheckIfNodesHaveBeenVisited(memberPath, leftNode, rightNode);

            VisitLeafNodes(memberPath, leftNode, rightNode);
        }

        private object InterceptNode(string memberPath, object node)
        {
            var interceptorsToRun = nodeInterceptors.Where(interceptor => interceptor.Use(memberPath, node));
            var result = node;
            foreach (var nodeInterceptor in interceptorsToRun)
            {
                result = nodeInterceptor.Intercept(result);
            }
            return result;
        }

        private static bool IsALeafNode(object node)
        {
            return node == null || node.IsAValue();
        }

        private void CompareLeafNode(string memberPath, object leftNode, object rightNode)
        {
            if (!AreEqual(leftNode, rightNode))
                report.ReportDiff(memberPath, leftNode, rightNode);
        }

        private static bool AreEqual(object leftValue, object rightValue)
        {
            if (leftValue == null && rightValue == null) return true;
            if (leftValue == null || rightValue == null) return false;

            if (leftValue.IsValueType() ||
                leftValue.IsEnum())
                return leftValue.Equals(rightValue);

            return true;
        }

        private void CheckIfNodesHaveBeenVisited(string memberPath, object leftNode, object rightNode)
        {
            if (NodesAreVisited(leftNode, rightNode))
            {
                var message = String.Format(
                    "It's not possible to diff graphs with cyclic dependencies. Found in: {0}",
                    memberPath);
                throw new Exception(message);
            }
            MarkNodesAsVisited(leftNode, rightNode);
        }

        private bool NodesAreVisited(object leftNode, object rightNode)
        {
            return visitedNodes.Contains(leftNode) ||
                   visitedNodes.Contains(rightNode);
        }

        private void MarkNodesAsVisited(object leftNode, object rightNode)
        {
            visitedNodes.Add(leftNode);
            visitedNodes.Add(rightNode);
        }

        private void VisitLeafNodes(string memberPath, object leftNode, object rightNode)
        {
            if (leftNode.IsEnumerable() && rightNode.IsEnumerable())
                VisitNodesInList(memberPath, leftNode as IEnumerable, rightNode as IEnumerable);

            VisitPublicFields(memberPath, leftNode, rightNode);
            VisitPublicProperties(memberPath, leftNode, rightNode);
        }

        private void VisitNodesInList(string currentMemberPath, IEnumerable leftList, IEnumerable rightList)
        {
            var leftCount = leftList.Count();
            var rightCount = rightList.Count();

            var largestCount = Math.Max(leftCount, rightCount);
            var leftEnumerator = leftList.GetEnumerator();
            var rightEnumerator = rightList.GetEnumerator();

            for (var i = 0; i < largestCount; i++)
            {
                var leftNode = GetNextValueOrNullFromList(leftEnumerator, i, leftCount);
                var rightNode = GetNextValueOrNullFromList(rightEnumerator, i, rightCount);
                var newMemberPath = currentMemberPath + "[" + i + "]";

                VisitNode(newMemberPath, leftNode, rightNode);
            }
        }

        private static object GetNextValueOrNullFromList(IEnumerator enumerator, int currentIndex, int enumeratorCount)
        {
            object currentValue;
            if (currentIndex < enumeratorCount)
            {
                enumerator.MoveNext();
                currentValue = enumerator.Current;
            }
            else
                currentValue = null;
            return currentValue;
        }

        private void VisitPublicFields(string currentMemberPath, object leftObject, object rightObject)
        {
            var leftFields = leftObject.PublicFields();
            var rightFields = rightObject.PublicFields();

            for (var i = 0; i < leftFields.Length; i++)
            {
                var fieldName = leftFields[i].Name;
                var leftValue = leftFields[i].GetValue(leftObject);
                var rightValue = rightFields[i].GetValue(rightObject);
                var newMemberPath = NewPath(currentMemberPath, fieldName);

                VisitNode(newMemberPath, leftValue, rightValue);
            }
        }

        private void VisitPublicProperties(string currentMemberPath, object leftObject, object rightObject)
        {
            var leftGetterProps = leftObject.PublicGetterProperties();
            var rightGetterProps = rightObject.PublicGetterProperties();

            Func<PropertyInfo[], int, PropertyInfo> tryGetProperty = (properties, index) =>
            {
                if (index < properties.Length) return properties[index];
                return null;
            };

            var numberOfProperties = leftGetterProps.Length > rightGetterProps.Length
                                         ? leftGetterProps.Length
                                         : rightGetterProps.Length;

            for (var i = 0; i < numberOfProperties; i++)
            {
                var leftProperty = tryGetProperty(leftGetterProps, i);
                var rightProperty = tryGetProperty(rightGetterProps, i);

                if (!leftProperty.Exists() && rightProperty.Exists())
                {
                    var newMemberPath = NewPath(currentMemberPath, rightProperty.Name);
                    var rightValue = rightProperty.GetValue(rightObject);
                    VisitNode(newMemberPath, null, rightValue);
                }
                else if (leftProperty.Exists() && !rightProperty.Exists())
                {
                    var newMemberPath = NewPath(currentMemberPath, leftProperty.Name);
                    var leftValue = leftProperty.GetValue(leftObject);
                    VisitNode(newMemberPath, leftValue, null);
                }
                else if (!leftProperty.IsIndexerProperty() &&
                    !rightProperty.IsIndexerProperty())
                {
                    var leftValue = leftProperty.GetValue(leftObject);
                    var rightValue = rightProperty.GetValue(rightObject);
                    var newMemberPath = NewPath(currentMemberPath, leftProperty.Name);
                    VisitNode(newMemberPath, leftValue, rightValue);
                }
            }
        }

        private static string NewPath(string currentPath, string name)
        {
            var prefixMember = (currentPath != "") ? currentPath + "." : "";
            return prefixMember + name;
        }

        private static DiffReport NoDiffFound()
        {
            return new DiffReport();
        }
    }
}
