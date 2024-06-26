﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGeneratorsKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Datapack.Net.SourceGenerator
{
    [Generator]
    public class ProjectGenerator : IIncrementalGenerator
    {
        public static readonly DiagnosticDescriptor InvalidFunctionFormat = new("MC0001", "Invalid Function", "Function {0} is not a valid Datapack function, and it must be private and its name must start with an underscore", "Datapack", DiagnosticSeverity.Error, true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var projects = context.SyntaxProvider.ForAttributeWithMetadataName<Project?>("Datapack.Net.CubeLib.ProjectAttribute",
                static (s, _) => true,
                static (ctx, _) =>
                {
                    //if (!Debugger.IsAttached)
                    //{
                    //    Debugger.Launch();
                    //}

                    var cls = (ClassDeclarationSyntax)ctx.TargetNode;

                    foreach (var i in cls.AttributeLists)
                    {
                        foreach (var e in i.Attributes)
                        {
                            if (ctx.SemanticModel.GetSymbolInfo(e).Symbol is not IMethodSymbol attribute) continue;
                            if (attribute.ContainingType.ToDisplayString() == "Datapack.Net.CubeLib.ProjectAttribute")
                            {
                                if (ctx.SemanticModel.GetDeclaredSymbol(cls) is not INamedTypeSymbol clsSymbol) return null;

                                List<MCFunction> funcs = [];
                                foreach (var sym in clsSymbol.GetMembers())
                                {
                                    if (sym is IMethodSymbol method && sym.GetAttributes().Where(i => i.AttributeClass.ToDisplayString().Contains("Datapack.Net.CubeLib.DeclareMC")).Count() != 0)
                                    {
                                        funcs.Add(Utils.GetMCFunction(method));
                                    }
                                }
                                return new Project(clsSymbol.Name, clsSymbol.ContainingNamespace.ToDisplayString(), funcs);
                            }
                        }
                    }
                    return null;
                }
            ).Where(static m => m is not null);

            context.RegisterSourceOutput(projects, static (spc, source) => Execute(source, spc));
        }

        private static void Execute(Project? _project, SourceProductionContext context)
        {
            if (_project is not { } project) return;

            var funcs = new StringBuilder();

            foreach (var i in project.Functions)
            {
                funcs.AppendLine(Utils.GenerateWrapper(i));
            }

            if (funcs.Length > 0) funcs.Length--;

            string source = $@"/// <auto-generated/>
namespace {project.Namespace}
{{
    public partial class {project.Name}
    {{
{funcs}
    }}
}}";
            context.AddSource($"{project.Name}.g.cs", source);
        }
    }
}
