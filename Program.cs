using System;
using System.Collections.Generic;
using System.Linq;
using Algenta.Colectica.Model;
using Algenta.Colectica.Model.Ddi;
using Algenta.Colectica.Model.Ddi.Serialization;
using Algenta.Colectica.Model.Utility;

namespace BlaiseFatBurner
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            MultilingualString.CurrentCulture = "en-GB";
            VersionableBase.DefaultAgencyId = "uk.closer";

            var file = args.Last();
            if (!System.IO.File.Exists(file))
            {
                Console.WriteLine("Cannot find file.");
                return;
            }

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i][0].Equals('-'))
                {
                    if (args[i].Equals(@"-l"))
                    {
                        MultilingualString.CurrentCulture = args[i + 1];
                        i++;
                        continue;
                    }
                    if (args[i].Equals(@"-a"))
                    {
                        VersionableBase.DefaultAgencyId = args[i + 1];
                        i++;
                        continue;
                    }
                }
            }


            var validator = new DdiValidator(file, DdiFileFormat.Ddi32);
            if (validator.Validate())
            {
                var doc = validator.ValidatedXDocument;
                var deserializer = new Ddi32Deserializer();
                var instance = deserializer.GetDdiInstance(doc.Root);
                var gatherer = new ItemGathererVisitor();
                instance.Accept(gatherer);
                var allItems = gatherer.FoundItems;

                var qis = allItems.OfType<Question>().Where(x => !x.QuestionText.IsEmpty).ToList();
                Console.WriteLine("{0} items.", allItems.Count);
                Console.WriteLine("{0} qis with text.", qis.Count);

                var newInstance = new DdiInstance();
                newInstance.ResourcePackages.Add(new ResourcePackage());
                var rp = newInstance.ResourcePackages.Last();
                rp.QuestionSchemes.Add(new QuestionScheme());
                var qs = rp.QuestionSchemes.Last();
                var cls = new Dictionary<Guid, CodeList>();
                var iis = new Dictionary<Guid, InterviewerInstruction>();
                foreach (var qi in qis)
                {
                    qs.Questions.Add(qi);
                    foreach (var cl in qi.GetChildren().OfType<CodeList>())
                    {
                        cls[cl.Identifier] = cl;
                    }
                    foreach (var ii in qi.GetChildren().OfType<InterviewerInstruction>())
                    {
                        iis[ii.Identifier] = ii;
                    }
                }

                rp.CodeListSchemes.Add(new CodeListScheme());
                var clScheme = rp.CodeListSchemes.Last();
                var cats = new Dictionary<Guid, Category>();
                foreach (var cl in cls)
                {
                    clScheme.CodeLists.Add(cl.Value);
                    foreach (var cat in cl.Value.GetChildren().OfType<Category>())
                    {
                        cats[cat.Identifier] = cat;
                    }
                }

                rp.CategorySchemes.Add(new CategoryScheme());
                var catScheme = rp.CategorySchemes.Last();
                foreach (var cat in cats)
                {
                    catScheme.Categories.Add(cat.Value);
                }

                rp.InterviewerInstructionSchemes.Add(new InterviewerInstructionScheme());
                var iiScheme = rp.InterviewerInstructionSchemes.Last();
                foreach (var ii in iis)
                {
                    iiScheme.InterviewerInstructions.Add(ii.Value);
                }

                rp.ControlConstructSchemes.Add(new ControlConstructScheme());
                var top = new CustomSequenceActivity();
                top.ItemName["en-GB"] = "top_sequence";
                rp.ControlConstructSchemes.Last().ControlConstructs.Add(top);

                var instrument = new Instrument();
                instrument.ItemName["en-GB"] = "MCS3";
                instrument.Sequence = top;
                rp.InstrumentSchemes.Add(new InstrumentScheme());
                rp.InstrumentSchemes.Last().Instruments.Add(instrument);

                var serializer = new Ddi32Serializer {UseConciseBoundedDescription = false};
                var output = serializer.Serialize(newInstance);
                output.Save(@"output.xml");
            }
            else
            {
                Console.WriteLine("The DDI-3.2 file is invalid for Colectica.");
            }
        }
    }
}