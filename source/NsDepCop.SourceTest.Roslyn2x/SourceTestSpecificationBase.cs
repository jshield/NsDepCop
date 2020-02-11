﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Codartis.NsDepCop.Core.Factory;
using Codartis.NsDepCop.Core.Interface.Analysis;
using Codartis.NsDepCop.Core.Interface.Analysis.Messages;
using Codartis.NsDepCop.TestUtil;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Codartis.NsDepCop.SourceTest
{
    internal abstract class SourceTestSpecificationBase : FileBasedTestsBase
    {
        protected virtual CSharpParseOptions CSharpParseOptions => null;

        private readonly string _name;
        private readonly ITypeDependencyEnumerator _typeDependencyEnumerator;
        private readonly List<SourceLineSegment> _invalidLineSegments = new List<SourceLineSegment>();

        protected SourceTestSpecificationBase(string name, ITypeDependencyEnumerator typeDependencyEnumerator)
        {
            _name = name;
            _typeDependencyEnumerator = typeDependencyEnumerator;
        }

        public SourceTestSpecificationBase ExpectInvalidSegment(int line, int startColumn, int endColumn)
        {
            _invalidLineSegments.Add(new SourceLineSegment(line, startColumn, endColumn));
            return this;
        }

        public void Execute()
        {
            var sourceFilePaths = new[] { _name }.Select(GetTestFileFullPath).ToList();
            var referencedAssemblyPaths = GetReferencedAssemblyPaths().ToList();

            ValidateCompilation(sourceFilePaths, referencedAssemblyPaths);
            AssertIllegalDependencies(sourceFilePaths, referencedAssemblyPaths);
        }

        protected static void DebugMessageHandler(string message) => Debug.WriteLine(message);

        private void ValidateCompilation(IEnumerable<string> sourceFiles, IEnumerable<string> referencedAssemblies)
        {
            var compilation = CSharpCompilation.Create(
                "NsDepCopProject",
                sourceFiles.Select(i => CSharpSyntaxTree.ParseText(LoadFile(i), CSharpParseOptions)),
                referencedAssemblies.Select(i => MetadataReference.CreateFromFile(i)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

            var errors = compilation.GetDiagnostics().Where(i => i.Severity == DiagnosticSeverity.Error).ToList();
            errors.Should().HaveCount(0);
        }

        private void AssertIllegalDependencies(IEnumerable<string> sourceFiles, IEnumerable<string> referencedAssemblies)
        {
            var baseFolder = GetBinFilePath(_name);
            var illegalDependencies = GetIllegalDependencies(baseFolder, sourceFiles, referencedAssemblies).ToList();

            illegalDependencies.Select(i => i.SourceSegment)
                .Should().Equal(_invalidLineSegments,
                (typeDependency, sourceLineSegment) => sourceLineSegment.Equals(typeDependency));
        }

        private IEnumerable<TypeDependency> GetIllegalDependencies(string baseFolder,
            IEnumerable<string> sourceFiles, IEnumerable<string> referencedAssemblies)
        {
            var dependencyAnalyzerFactory = new DependencyAnalyzerFactory(DebugMessageHandler); 
            var dependencyAnalyzer = dependencyAnalyzerFactory.CreateInProcess(baseFolder, _typeDependencyEnumerator);
            return dependencyAnalyzer.AnalyzeProject(sourceFiles, referencedAssemblies).OfType<IllegalDependencyMessage>().Select(i => i.IllegalDependency);
        }

        private static string GetTestFileFullPath(string testName)
        {
            return Path.Combine(GetBinFilePath($@"{testName}\{testName}.cs"));
        }

        private static IEnumerable<string> GetReferencedAssemblyPaths()
        {
            return new[]
            {
                // mscorlib
                Assembly.Load("System.Runtime").Location,
                GetAssemblyPath(typeof(System.Object).Assembly),
                GetAssemblyPath(typeof(System.Decimal).Assembly),
                // rewrite to use assembly loader mechanism for .net core?
                GetAssemblyPath(typeof(FileInfo).Assembly),
                GetAssemblyPath(typeof(System.Console).Assembly)
            };
        }
    }
}
