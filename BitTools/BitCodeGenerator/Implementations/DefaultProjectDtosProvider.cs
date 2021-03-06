﻿using System;
using System.Collections.Generic;
using System.Linq;
using BitTools.Core.Contracts;
using BitTools.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using System.Threading.Tasks;

namespace BitCodeGenerator.Implementations
{
    public class DefaultProjectDtosProvider : IProjectDtosProvider
    {
        private readonly IProjectDtoControllersProvider _projectDtoControllersProvider;

        public DefaultProjectDtosProvider(IProjectDtoControllersProvider projectDtoControllersProvider)
        {
            if (projectDtoControllersProvider == null)
                throw new ArgumentNullException(nameof(projectDtoControllersProvider));

            _projectDtoControllersProvider = projectDtoControllersProvider;
        }

        static DefaultProjectDtosProvider()
        {
            _isvPropertyForISyncableDtos = new Lazy<Task<IPropertySymbol>>(async () =>
            {
                ProjectId projectId = ProjectId.CreateNewId(debugName: "ISVPropertyProject");
                DocumentId isvPropDocId = DocumentId.CreateNewId(projectId, debugName: "ISVProp.cs");

                Solution solution = new AdhocWorkspace()
                    .CurrentSolution
                    .AddProject(projectId, "ISVPropertyProject", "ISVPropertyProject", LanguageNames.CSharp)
                    .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(bool).Assembly.Location))
                    .AddDocument(isvPropDocId, "ISVProp.cs", SourceText.From(@"
public class ISVClass
{
    public bool ISV { get; set; }
}"
                    ));

                Document isvPropDoc = solution.Projects.Single().Documents.Single();
                ClassDeclarationSyntax isvPropClassDec = (await isvPropDoc.GetSyntaxRootAsync()).DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
                ITypeSymbol isvPropTypeSymbol = (await isvPropDoc.GetSemanticModelAsync()).GetDeclaredSymbol(isvPropClassDec);
                return isvPropTypeSymbol.GetMembers().OfType<IPropertySymbol>().Single();

            });
        }

        private static readonly Lazy<Task<IPropertySymbol>> _isvPropertyForISyncableDtos;

        public virtual async Task<IList<Dto>> GetProjectDtos(Project project, IList<Project> allSourceProjects = null)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            if (allSourceProjects == null)
                allSourceProjects = new List<Project> { project };

            List<DtoController> dtoControllers = (await _projectDtoControllersProvider
                .GetProjectDtoControllersWithTheirOperations(project)).ToList();

            IList<Dto> dtos = new List<Dto>();

            foreach (DtoController dtoController in dtoControllers)
            {
                Dto dto = new Dto
                {
                    DtoSymbol = (INamedTypeSymbol)dtoController.ModelSymbol,
                    Properties = dtoController.ModelSymbol.GetMembers().OfType<IPropertySymbol>().ToList()
                };

                if (dto.DtoSymbol.Interfaces.Any(i => i.Name == "ISyncableDto") && !dto.Properties.Any(p => p.Name == "ISV"))
                {
                    dto.Properties.Add(await _isvPropertyForISyncableDtos.Value);
                }

                dtos.Add(dto);
            }

            List<Compilation> sourceProjectsCompilations = new List<Compilation>();

            foreach (var p in allSourceProjects)
            {
                sourceProjectsCompilations.Add(await p.GetCompilationAsync());
            }

            dtos.SelectMany(d => d.Properties)
                .Where(p => p.Type.IsComplexType())
                .Select(p => p.Type.GetUnderlyingComplexType())
                .Union(dtoControllers.SelectMany(dtoController => dtoController.Operations.SelectMany(operation => operation.Parameters.Select(p => p.Type).Union(new[] { operation.ReturnType }))).Where(t => t.IsComplexType()).Select(t => t.GetUnderlyingComplexType()))
                .Where(complexType => sourceProjectsCompilations.Any(c => c.Assembly.TypeNames.Any(tName => tName == complexType.Name)))
                .Distinct()
                .ToList()
                .ForEach(complexType =>
                {
                    dtos.Add(new Dto
                    {
                        DtoSymbol = (INamedTypeSymbol)complexType,
                        Properties = complexType.GetMembers().OfType<IPropertySymbol>().ToList()
                    });
                });

            dtos.ToList()
                .ForEach(dto =>
                {
                    if (dto.DtoSymbol.BaseType.IsDto())
                    {
                        dto.BaseDtoSymbol = dto.DtoSymbol.BaseType;
                    }
                });

            List<Dto> orderedDtos = new List<Dto>();

            dtos.ToList()
                .ForEach(dto =>
                {
                    int insertIndex = 0;

                    var orderedDtosWithIndex = orderedDtos.Select((d, i) => new { Dto = d, Index = i }).ToList();

                    foreach (var dWithIndex in orderedDtosWithIndex)
                    {
                        if (dto.BaseDtoSymbol?.Equals(dWithIndex.Dto.DtoSymbol) == true)
                            insertIndex = dWithIndex.Index + 1;
                    }

                    orderedDtos.Insert(insertIndex, dto);
                });

            return orderedDtos;
        }
    }
}
