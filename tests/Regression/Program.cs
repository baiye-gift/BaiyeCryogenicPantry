using System;
using System.Collections.Generic;
using Baiye.CryogenicPantry.Core;

int count = 0;
void Check(string name, bool ok) { if (!ok) throw new Exception("FAIL: " + name); count++; }
bool Near(double a, double b) => Math.Abs(a-b) < .0001;
// User-visible startup contract: one warm 500 kg stack, 20 C food and cabinet,
// 30 C exterior, actual float item temperature, full payment, no door events.
float loadedFood=293.15f;double loadedShell=293.15,loadCapacity=2000,loadInitial=loadedFood*loadCapacity+loadedShell*PantryRules.ShellHeatCapacity,loadLeak=0,loadRemoved=0;bool loadCooling=false;
for(int tick=0;tick<1800;tick++)
{
    loadCooling=loadedShell>PantryRules.TargetK+.75f||(loadCooling&&loadedShell>PantryRules.TargetK+.02f);
    float loadRate=PantryRules.CoolingRate(loadCooling,loadedShell>PantryRules.TargetK+.1f);
    double leak=(303.15-loadedShell)*PantryRules.InsulationKWPerK*.2;loadedShell+=leak/PantryRules.ShellHeatCapacity;loadLeak+=leak;
    double transfer=PantryRules.ExchangeEnergy(loadedFood,loadCapacity,loadedShell,.2);float before=loadedFood;loadedFood=(float)(before-transfer/loadCapacity);
    loadedShell+=((double)before-loadedFood)*loadCapacity/PantryRules.ShellHeatCapacity;
    double extracted=PantryRules.CoolingEnergy(loadedShell,PantryRules.ShellHeatCapacity,PantryRules.TargetK,loadRate*.2);
    loadedShell-=extracted/PantryRules.ShellHeatCapacity;loadRemoved+=extracted;
}
Check("500 kg 20 C food really reaches deep freeze within 360 paid seconds",loadedFood<255.15f&&loadedShell<255.15);
Check("fast startup still balances actual float heat",Math.Abs(loadInitial+loadLeak-loadedFood*loadCapacity-loadedShell*PantryRules.ShellHeatCapacity-loadRemoved)<.001);
Check("cooling extracts paid heat", Near(PantryRules.CoolingEnergy(300, 10, 250, 24),24));
Check("cooling cannot overshoot target", Near(PantryRules.CoolingEnergy(251, 2, 250, 24),2));
Check("already cold does not extract heat", PantryRules.CoolingEnergy(249, 10, 250, 24)==0);
Check("zero capacity safe", PantryRules.CoolingEnergy(300,0,250,24)==0);
Check("negative budget safe", PantryRules.CoolingEnergy(300,10,250,-1)==0);
double shell=251.15f,food=300,c=2000;
double exchange=PantryRules.ExchangeEnergy(food,c,shell,.2f);
double energyBefore=food*c+shell*PantryRules.ShellHeatCapacity;
food-=exchange/c;shell+=exchange/PantryRules.ShellHeatCapacity;
Check("warm insertion warms cabinet",shell>251.15f&&food<300);
Check("item and shell exchange conserve heat",Math.Abs(energyBefore-(food*c+shell*PantryRules.ShellHeatCapacity))<.1);
Check("paused simulation exchanges no heat",PantryRules.ExchangeEnergy(300,c,251.15f,0)==0);
Check("cold food cools warmer cabinet",PantryRules.ExchangeEnergy(240,c,270,.2f)<0);
double warmShell=shell;
double unpoweredLeak=(293.15f-shell)*PantryRules.InsulationKWPerK*10;
shell+=unpoweredLeak/PantryRules.ShellHeatCapacity;
Check("unpowered cabinet warms gradually",shell>warmShell&&shell<293.15f);
Check("off preserves thermal inertia rather than resetting",shell<293.15f&&Math.Abs(shell-warmShell)<.1);
double initial=food*c+shell*PantryRules.ShellHeatCapacity,removed=0;
for(int tick=0;tick<25000;tick++)
{
    double q=PantryRules.ExchangeEnergy(food,c,shell,.2f);food-=q/c;shell+=q/PantryRules.ShellHeatCapacity;
    double cool=PantryRules.CoolingEnergy(shell,PantryRules.ShellHeatCapacity,PantryRules.TargetK,PantryRules.CoolingKW*.2f);
    shell-=cool/PantryRules.ShellHeatCapacity;removed+=cool;
}
Check("500 kg warm load eventually cools",food<252&&shell<252);
Check("all cooling heat has an accounted external destination",Math.Abs(initial-(food*c+shell*PantryRules.ShellHeatCapacity)-removed)<.001);
float nativeFood=300;double preciseShell=251.15;double nativeInitial=nativeFood*c+preciseShell*PantryRules.ShellHeatCapacity,nativeRemoved=0;
for(int tick=0;tick<25000;tick++)
{
    double q=PantryRules.ExchangeEnergy(nativeFood,c,preciseShell,.2);float before=nativeFood;nativeFood=(float)(nativeFood-q/c);
    q=((double)before-nativeFood)*c;preciseShell+=q/PantryRules.ShellHeatCapacity;
    double cool=PantryRules.CoolingEnergy(preciseShell,PantryRules.ShellHeatCapacity,PantryRules.TargetK,PantryRules.CoolingKW*.2);
    preciseShell-=cool/PantryRules.ShellHeatCapacity;nativeRemoved+=cool;
}
Check("native float item and double shell conserve actual heat",Math.Abs(nativeInitial-(nativeFood*c+preciseShell*PantryRules.ShellHeatCapacity)-nativeRemoved)<.001);
Check("partial power scales work", Near(PantryRules.PaidFraction(60,600,.2f),.5f));
Check("overpayment cannot create extra work", PantryRules.PaidFraction(200,600,.2f)==1);
Check("zero dt gives no free work", PantryRules.PaidFraction(200,600,0)==0);
Check("zero watts gives no free work", PantryRules.PaidFraction(200,0,.2f)==0);
Check("reserve maintained at boundary", PantryRules.CanSpend(20.2f,.2f,20));
Check("reserve rejects insufficient stock", !PantryRules.CanSpend(20.19f,.2f,20));
Check("empty inventory rejects recipe", !PantryRules.CanSpend(0,.2f,0));
Check("full tank can replace with paid incoming gas", Near(PantryRules.ReplacementIntake(2,0,.02f),.02f));
Check("correct full atmosphere consumes no gas", PantryRules.ReplacementIntake(2,2,.02f)==0);
Check("door loss real and bounded", Near(PantryRules.DoorLoss(2),.03f));
Check("no tank creates no loss", PantryRules.DoorLoss(0)==0);
Check("standby power", PantryRules.Power(false,false,false)==120);
Check("maintenance power", PantryRules.Power(false,true,false)==240);
Check("maximum with drinks", PantryRules.Power(true,false,true)==720);
Check("standby has no unpaid cooling",PantryRules.CoolingRate(false,false)==0);
Check("maintenance cooling budget",Near(PantryRules.CoolingRate(false,true),96));
Check("active cooling budget",PantryRules.CoolingRate(true,true)==240);
Check("copper condensation excludes metal vapour",!PantryRules.GasPhaseAllowed(true,true,2834,10000));
Check("steam condensation excludes hot-only gas",!PantryRules.GasPhaseAllowed(true,true,372.5f,10000));
Check("room temperature oxygen is still not sterile",!PantryRules.GasPhaseAllowed(true,false,90.19f,10000));
Check("liquid phase never becomes a gas candidate",!PantryRules.GasPhaseAllowed(false,true,225,10000));
Check("gas unstable at target is excluded",!PantryRules.GasPhaseAllowed(true,true,PantryRules.TargetK,10000));
Check("invalid phase metadata is excluded",!PantryRules.GasPhaseAllowed(true,true,float.NaN,10000)&&!PantryRules.GasPhaseAllowed(true,true,225,float.NaN));
Check("50 C normal gas intake boundary allowed",PantryRules.GasIntakeAllowed(PantryRules.MaxGasIntakeK,225,10000));
Check("hot gas rejected before consuming pipe mass",!PantryRules.GasIntakeAllowed(323.16f,225,10000)&&!PantryRules.GasIntakeAllowed(1000,225,10000));
Check("cold CO2 below phase threshold rejected",!PantryRules.GasIntakeAllowed(200,225,10000));
Check("nonfinite packet temperature rejected",!PantryRules.GasIntakeAllowed(float.NaN,225,10000)&&!PantryRules.GasIntakeAllowed(float.PositiveInfinity,225,10000));
bool Supported(int id)=>id==1||id==2||id==3;
Check("old invalid target recovers to actual stored gas",PantryRules.RecoverGasSelection(99,2,1,Supported)==2);
Check("valid saved target stays selected even during replacement",PantryRules.RecoverGasSelection(3,2,1,Supported)==3);
Check("invalid target and old vapour inventory recover to CO2",PantryRules.RecoverGasSelection(99,98,1,Supported)==1);
var replacementStock=new[]{new KeyValuePair<int,float>(2,1.8f),new KeyValuePair<int,float>(1,.2f)};
Check("cancel restores old gas even after new gas became majority",PantryRules.ReplacementReturnTarget(2,replacementStock,Supported)==1);
Check("cancel with old majority restores that old gas",PantryRules.ReplacementReturnTarget(1,replacementStock,Supported)==2);
Check("cancel cannot select unsupported old vapour",PantryRules.ReplacementReturnTarget(1,new[]{new KeyValuePair<int,float>(99,2)},Supported)==null);
Check("empty tank has no phantom cancellation target",PantryRules.ReplacementReturnTarget(1,Array.Empty<KeyValuePair<int,float>>(),Supported)==null);
Check("cancel policy never changes real stock",replacementStock[0].Value==1.8f&&replacementStock[1].Value==.2f);
Check("CO2 atmosphere allows paid processing",PantryRules.ProcessAllowed(true,true,true,.95f));
Check("changing to chlorine pauses existing batch",!PantryRules.ProcessAllowed(true,true,false,1));
Check("incomplete CO2 replacement pauses batch",!PantryRules.ProcessAllowed(true,true,true,.94f));
Check("global production pause stops batch",!PantryRules.ProcessAllowed(false,true,true,1));
Check("recipe pause stops batch",!PantryRules.ProcessAllowed(true,false,true,1));
Check("four initial recipes", DrinkRecipes.All.Count==4);
foreach (var r in DrinkRecipes.All) Check(r.Id+" conserved batch mass", Near(r.Mass,1));
bool duplicate=false; try { DrinkRecipes.Register(DrinkRecipes.All[0]); } catch(ArgumentException) { duplicate=true; }
Check("stable IDs reject duplicate",duplicate);
bool invalid=false; try { DrinkRecipes.Register(new DrinkRecipe("x","x","x","x",1,0,20)); } catch(ArgumentException) { invalid=true; }
Check("invalid recipe rejects zero CO2",invalid);
bool nonfinite=false;try { DrinkRecipes.Register(new DrinkRecipe("nan","x","x","x",float.NaN,.03f,20)); }catch(ArgumentException){nonfinite=true;}
Check("extension rejects nonfinite resource amounts",nonfinite);
bool overlap=false;try { DrinkRecipes.Register(new DrinkRecipe("overlap","x","x","x",.5f,.03f,20,new Ingredient("PrickleFruit",.2f),new Ingredient("PrickleFruit",.2f))); }catch(ArgumentException){overlap=true;}
Check("extension rejects duplicate ingredients bypassing reserves",overlap);
Check("short side panel never forces 240 px",PantryPanelLayout.Height(100,800)==100);
Check("long page is bounded to remaining side space",PantryPanelLayout.Height(280,800)==280);
Check("long page uses compact 360 px cap",PantryPanelLayout.Height(1000,800)==360);
Check("short page shrinks to natural height",PantryPanelLayout.Height(1000,70)==70+PantryPanelLayout.ChromeHeight);
Check("no available space does not grow a panel",PantryPanelLayout.Height(0,800)==0&&PantryPanelLayout.Height(-20,800)==0);
Check("invalid layout measurements stay finite",PantryPanelLayout.Height(float.NaN,800)==0&&PantryPanelLayout.Height(200,float.NaN)==PantryPanelLayout.ChromeHeight);
foreach(float scale in new[]{1f,1.25f,1.5f,2f})
{
    float physical=420-18*scale-60*scale;
    float height=PantryPanelLayout.Height(physical/scale,800);
    Check("side panel pixel budget at scale "+scale,height*scale<=physical+.001f);
}
for(int budget=50;budget<=800;budget+=50)
    Check("expanded recipes respect canvas budget "+budget,PantryPanelLayout.Height(budget,2000)<=budget&&PantryPanelLayout.Height(budget,2000)<=PantryPanelLayout.MaxHeight);
Console.WriteLine($"PASS {count} core behavior checks");
