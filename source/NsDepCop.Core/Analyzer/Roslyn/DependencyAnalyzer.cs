﻿using Codartis.NsDepCop.Core.Common;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;

namespace Codartis.NsDepCop.Core.Analyzer.Roslyn
{
    /// <summary>
    /// Dependency analyzer implemented with Roslyn.
    /// </summary>
    public class DependencyAnalyzer : IDependencyAnalyzer
    {
        private readonly NsDepCopConfig _config;
        private readonly DependencyValidator _dependencyValidator;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="config">Config object.</param>
        public DependencyAnalyzer(NsDepCopConfig config)
        {
            _config = config;
            _dependencyValidator = new DependencyValidator(config.AllowedDependencies, config.DisallowedDependencies,
                config.ChildCanDependOnParentImplicitly);
        }

        /// <summary>
        /// Gets the name of the parser.
        /// </summary>
        public string ParserName => "Roslyn";

        /// <summary>
        /// Analyses a project (source files and referenced assemblies) and returns the found dependency violations.
        /// </summary>
        /// <param name="baseDirectory">The full path of the base directory of the project.</param>
        /// <param name="sourceFilePaths">A collection of the full path of source files.</param>
        /// <param name="referencedAssemblyPaths">A collection of the full path of referenced assemblies.</param>
        /// <returns>A collection of dependency violations. Empty collection if none found.</returns>
        public IEnumerable<DependencyViolation> AnalyzeProject(
            string baseDirectory,
            IEnumerable<string> sourceFilePaths,
            IEnumerable<string> referencedAssemblyPaths)
        {
            var referencedAssemblies = referencedAssemblyPaths.Select(i => MetadataReference.CreateFromFile(i)).ToList();
            var syntaxTrees = sourceFilePaths.Select(ParseFile).ToList();
            var compilation = CSharpCompilation.Create("NsDepCopTaskProject", syntaxTrees, referencedAssemblies);

            foreach (var syntaxTree in syntaxTrees)
            {
                var syntaxVisitor = new DependencyAnalyzerSyntaxVisitor(compilation.GetSemanticModel(syntaxTree), _config, _dependencyValidator);
                var documentRootNode = syntaxTree.GetRoot();
                if (documentRootNode != null)
                {
                    var dependencyViolationsInDocument = syntaxVisitor.Visit(documentRootNode);
                    foreach (var dependencyViolation in dependencyViolationsInDocument)
                        yield return dependencyViolation;
                }
            }

            Debug.WriteLine(string.Format("Cache hits: {0}, cache misses:{1}, efficiency (hits/all): {2:P}",
                _dependencyValidator.CacheHitCount,
                _dependencyValidator.CacheMissCount,
                _dependencyValidator.CacheEfficiencyPercent),
                Constants.TOOL_NAME);
        }

        private static SyntaxTree ParseFile(string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var streamReader = new StreamReader(stream))
            {
                var sourceText = streamReader.ReadToEnd();
                return CSharpSyntaxTree.ParseText(sourceText, null, fileName);
            }
        }
    }
}
