using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Albo1125.Common.CommonLibrary;
using AssortedCallouts.Extensions;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;

namespace AssortedCallouts.Callouts
{
    [CalloutInfo("HotPursuit", CalloutProbability.Medium)]
    internal class HotPursuit : AssortedCallout
    {
        private bool _calloutRunning = false;
        private SuspectStates _suspectState;
        private string _message;
        private ModelWithName _selectedModelInfo;
        private static readonly List<Model> _carsToSelectFrom = new List<Model>
        {
            new Model("JESTER"), new Model("MASSACRO"), new Model("NINEF"), new Model("RAPIDGT"), new Model("COMET2"), new Model("ELEGY2"),
            new Model("FELTZER2"), new Model("SURANO"), new Model("CARBONIZZARE"), new Model("FELON"), new Model("FUSILADE"),
            new Model("ASTEROPE"), new Model("INGOT"), new Model("INTRUDER"), new Model("ORACLE"), new Model("PREMIER"), new Model("PRIMO"),
            new Model("SCHAFTER2"), new Model("SCHAFTER3"), new Model("STANIER"), new Model("STRATUM"), new Model("SUPERD"), new Model("TAILGATER"),
            new Model("WASHINGTON"), new Model("BALLER"), new Model("CAVALCADE"), new Model("GRESLEY"), new Model("HUNTLEY"), new Model("LANDSTALKER"),
            new Model("MESA"), new Model("PATRIOT"), new Model("RADI"), new Model("ROCOTO"), new Model("SEMINOLE"), new Model("SERRANO"), new Model("XLS"),
            new Model("EXEMPLAR"), new Model("F620"), new Model("FELON"), new Model("JACKAL"), new Model("ORACLE2"), new Model("SENTINEL"), new Model("WINDSOR"),
            new Model("WINDSOR2"), new Model("BLADE"), new Model("BUCCANEER"), new Model("DOMINATOR"), new Model("GAUNTLET"), new Model("PHOENIX"),
            new Model("PICADOR"), new Model("RUINER"), new Model("SABREGT"), new Model("STALION"), new Model("VIGERO"), new Model("VIRGO"),
            new Model("BFINJECTION"), new Model("BIFTA"), new Model("BODHI2"), new Model("DUBSTA"), new Model("DUNE"), new Model("BJXL"),
            new Model("SANDKING"), new Model("REBEL"), new Model("SANDKING2"), new Model("BISON"), new Model("BOBCATXL"), new Model("BURRITO"),
            new Model("GANGBURRITO"), new Model("MINIVAN"), new Model("PARADISE"), new Model("PONY"), new Model("RUMPO"), new Model("SURFER"),
            new Model("YOUGA"), new Model("BENSON"), new Model("BULLDOZER"), new Model("CUTTER"), new Model("DUMP"), new Model("FLATBED"),
            new Model("MIXER"), new Model("RUBBLE"), new Model("TIPTRUCK"), new Model("TIPTRUCK2"), new Model("SCRAP"), new Model("TOWTRUCK"),
            new Model("TOWTRUCK2")
        };
        
        private List<ModelWithName> _modelsWithDisplayNames = new List<ModelWithName>();

        private enum SuspectStates
        {
            InPursuit,
            Arrested,
            Dead,
            Escaped
        }

        private void InitialiseModelsWithDisplayNames()
        {
            _modelsWithDisplayNames = new List<ModelWithName>()
            {
                new ModelWithName(_carsToSelectFrom.ToArray(), "a vehicle")
            };
        }

        public override bool OnBeforeCalloutDisplayed()
        {
            Game.LogTrivial("AssortedCallouts.HotPursuit");

            // Initialize models
            InitialiseModelsWithDisplayNames();

            // Find a suitable spawn point
            SpawnPoint = World.GetNextPositionOnStreet(Game.LocalPlayer.Character.Position.Around(225f, 320f));

            if (SpawnPoint == Vector3.Zero)
            {
                Game.LogTrivial("Unable to find a valid spawn point.");
                return false;
            }

            // Choose a random model for the suspect
            _selectedModelInfo = _modelsWithDisplayNames[AssortedCalloutsHandler.rnd.Next(_modelsWithDisplayNames.Count)];
            _selectedModelInfo.ChosenModel.LoadAndWait();

            // Set up callout message and blip
            CalloutMessage = "Hot Pursuit of " + _selectedModelInfo.Name;
            CalloutPosition = SpawnPoint;
            ShowCalloutAreaBlipBeforeAccepting(SpawnPoint, 70f);

            // Dispatch audio
            Functions.PlayScannerAudioUsingPosition("DISP_ATTENTION_UNIT " + AssortedCalloutsHandler.DivisionUnitBeatAudioString + " WE_HAVE_01 CRIME_RESIST_ARREST IN_OR_ON_POSITION", SpawnPoint);

            return base.OnBeforeCalloutDisplayed();
        }

        public override bool OnCalloutAccepted()
        {
            // Create the suspect vehicle
            SuspectCar = new Vehicle(_selectedModelInfo.ChosenModel, SpawnPoint, SpawnPoint.Heading);
            SuspectCar.RandomiseLicencePlate();
            SuspectCar.IsPersistent = true;
            SuspectCar.IsEngineOn = true;

            // Create the suspect ped
            Suspect = SuspectCar.CreateRandomDriver();
            Suspect.MakeMissionPed();
            Suspect.WarpIntoVehicle(SuspectCar, -1);
            NativeFunction.Natives.SET_VEHICLE_FORWARD_SPEED(Suspect.CurrentVehicle, 25f);
            Suspect.Tasks.CruiseWithVehicle(60f, VehicleDrivingFlags.Emergency);

            // Start the callout handler
            CalloutHandler();

            return base.OnCalloutAccepted();
        }

        private void CalloutHandler()
        {
            _calloutRunning = true;
            GameFiber.StartNew(() =>
            {
                try
                {
                    // Handle dynamic suspect behavior
                    HandleDynamicSuspectBehavior();

                    // Display code 4 message
                    DisplayCodeFourMessage();
                }
                catch (System.Threading.ThreadAbortException)
                {
                    End();
                }
                catch (Exception e)
                {
                    if (_calloutRunning)
                    {
                        Game.LogTrivial(e.ToString());
                        Game.LogTrivial("Error occurred during callout execution.");
                        Game.DisplayNotification("~r~An error occurred during the callout. Please check your log file.");
                        End();
                    }
                }
            });
        }

        private void HandleDynamicSuspectBehavior()
        {
            // Add dynamic behavior here (e.g., evading through alleys, driving erratically, escaping on foot)
        }

        private void DisplayCodeFourMessage()
        {
            if (_calloutRunning)
            {
                GameFiber.Sleep(4000);
                Game.DisplayNotification(_message);
                Functions.PlayScannerAudio("ATTENTION_THIS_IS_DISPATCH_HIGH NO_FURTHER_UNITS_REQUIRED");
                CalloutFinished = true;
                End();
            }
        }

        public override void End()
        {
            _calloutRunning = false;

            // Play officer down audio if player character is dead
            if (Game.LocalPlayer.Character.Exists() && Game.LocalPlayer.Character.IsDead)
            {
                GameFiber.Wait(1500);
                Functions.PlayScannerAudio("OFFICER_HAS_BEEN_FATALLY_SHOT NOISE_SHORT OFFICER_NEEDS_IMMEDIATE_ASSISTANCE");
                GameFiber.Wait(3000);
            }

            // Dismiss or delete suspect
            if (CalloutFinished)
            {
                if (Suspect.Exists()) { Suspect.Dismiss(); }
            }
            else
            {
                if (Suspect.Exists()) { Suspect.Delete(); }
            }

            base.End();
        }

        private class ModelWithName
        {
            public Model ChosenModel;
            public string Name;

            public ModelWithName(Model[] models, string name)
            {
                ChosenModel = models[AssortedCalloutsHandler.rnd.Next(models.Length)];
                Name = name;
            }
        }
    }
}
