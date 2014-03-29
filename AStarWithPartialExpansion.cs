﻿using System.Collections.Generic;
using System.IO;

namespace CPF_experiment
{
    class AStarWithPartialExpansionBasic : ClassicAStar
    {
        int generatedAndDiscarded;
        bool hasMoreSuc;

        public override void Setup(ProblemInstance problemInstance) 
        { 
            base.Setup(problemInstance);
            generatedAndDiscarded = 0;
        }

        override public string GetName() { return "PE-Basic "; }

        override public bool Expand(WorldState simpleLookingNode)
        {
            WorldStateForPartialExpansion node = (WorldStateForPartialExpansion)simpleLookingNode;
            //Debug.Print("Expanding node " + node);
            if (node.notExpanded)
            {
                node.notExpanded = false;
                this.expandedFullStates++;
            }

            hasMoreSuc = false;
            node.nextFvalue = byte.MaxValue;

            expand(node, 0, runner, node.h + node.g, new HashSet<TimedMove>());
            node.h = node.nextFvalue - node.g;

            if (hasMoreSuc && node.h + node.g <= this.maxCost)
                this.openList.Add(node);
            return true;
        }

        /// <summary>
        /// Another recursive implementation!
        /// </summary>
        /// <param name="currentNode"></param>
        /// <param name="agentIndex"></param>
        /// <param name="runner"></param>
        /// <param name="targetF"></param>
        /// <param name="currentMoves"></param>
        /// <returns></returns>
        protected bool expand(WorldStateForPartialExpansion currentNode, int agentIndex, Run runner, int targetF, HashSet<TimedMove> currentMoves)
        {
            if (runner.ElapsedMilliseconds() > Constants.MAX_TIME)
                return true;
            WorldStateForPartialExpansion prev = (WorldStateForPartialExpansion)currentNode.prevStep;
            if (agentIndex == 0) // If this is the first agent that moves
            {
                hasMoreSuc = false;
                prev = currentNode;
                currentMoves.Clear();  
            }
            if (agentIndex == instance.m_vAgents.Length) // If all the agents have moved
            {

                currentNode.h = (int)this.heuristic.h(currentNode);
                currentNode.makespan++;
                currentNode.CalculateG();
                if (currentNode.h + currentNode.g <= this.maxCost)
                {
                    if (currentNode.h + currentNode.g == targetF)
                    {
                        if (instance.parameters.ContainsKey(Trevor.CONFLICT_AVOIDENCE))
                        {
                            currentNode.potentialConflictsCount = currentNode.prevStep.potentialConflictsCount;
                            currentNode.potentialConflictsCount += currentNode.conflictsCount(((HashSet<TimedMove>)instance.parameters[Trevor.CONFLICT_AVOIDENCE]));
                        }

                        if (instance.parameters.ContainsKey(CBS_LocalConflicts.INTERNAL_CAT))
                        {
                            currentNode.cbsInternalConflictsCount = currentNode.prevStep.cbsInternalConflictsCount;
                            currentNode.cbsInternalConflictsCount = currentNode.conflictsCount(((HashSet<TimedMove>)instance.parameters[CBS_LocalConflicts.INTERNAL_CAT]));
                        }

                        //if in closed list
                        if (this.closedList.ContainsKey(currentNode) == true)
                        {
                            WorldState inClosedList = this.closedList[currentNode];

                            //if g is smaller than remove the old world state
                            if (inClosedList.g > currentNode.g)
                            {
                                closedList.Remove(inClosedList);
                                openList.Remove(inClosedList);
                            }
                        }
                        if (this.closedList.ContainsKey(currentNode) == false)
                        {

                            this.generated++;
                            this.closedList.Add(currentNode, currentNode);

                            this.openList.Add(currentNode);
                            return true;
                        }
                    }
                    else
                        generatedAndDiscarded++;
                    if (currentNode.h + currentNode.g > targetF)
                    {
                        hasMoreSuc = true;
                        var prevStep = (WorldStateForPartialExpansion)currentNode.prevStep;
                        if (currentNode.h + currentNode.g < prevStep.nextFvalue)
                            prevStep.nextFvalue = (byte)(currentNode.h + currentNode.g);
                    }
                    return false;
                }
                return false;
            }

            // Try all legal moves of the agents
            CbsConstraint nextStepLocation = new CbsConstraint();
            bool ans = false;

            foreach (TimedMove agentLocation in currentNode.allAgentsState[agentIndex].last_move.GetNextMoves(Constants.ALLOW_DIAGONAL_MOVE))
            {
                if (this.constraintList != null)
                {
                    nextStepLocation.init(instance.m_vAgents[agentIndex].agent.agentNum, agentLocation);
                    if (constraintList.Contains(nextStepLocation))
                        continue;
                }
                if (IsValid(agentLocation, currentMoves))
                {
                    currentMoves.Add(agentLocation);
                    var childNode = new WorldStateForPartialExpansion(currentNode);
                    childNode.allAgentsState[agentIndex].move(agentLocation);
                    childNode.prevStep = prev;
                    if (expand(childNode, agentIndex + 1, runner, targetF, currentMoves))
                        ans = true;
                    currentMoves.Remove(agentLocation);
                }
            }
            return ans;
        }

        public override void OutputStatistics(TextWriter output)
        {
            output.Write(this.expanded + Run.RESULTS_DELIMITER);
            output.Write(this.generated + Run.RESULTS_DELIMITER);
            output.Write("N/A" + Run.RESULTS_DELIMITER);
            output.Write(this.generatedAndDiscarded + Run.RESULTS_DELIMITER);
            output.Write(solutionDepth + Run.RESULTS_DELIMITER);
            output.Write(expandedFullStates + Run.RESULTS_DELIMITER);
            output.Write("NA"/*Process.GetCurrentProcess().VirtualMemorySize64*/ + Run.RESULTS_DELIMITER);
            // Isn't there a CSV module in C# instead of fussing with the delimeter everywhere?
        }
    }
    

    class AStarWithPartialExpansion : ClassicAStar 
    {
        sbyte[][] fLookup;

        override protected WorldState CreateSearchRoot()
        {
            WorldStateForPartialExpansion root = new WorldStateForPartialExpansion(this.instance.m_vAgents);
            return root;
        }

        override public string GetName() { return "PEA* "; }

        public override bool Expand(WorldState nodeP)
        {
            WorldStateForPartialExpansion node = (WorldStateForPartialExpansion)nodeP;
            fLookup = null;

            byte[][] allMoves = node.getSingleAgentMoves(instance);
            int maxFchange = node.getMaxFchange(allMoves);

            if (node.isAlreadyExpanded() == false)
            {
                expandedFullStates++;
                node.alreadyExpanded = true;
            }
            //Debug.Print("Expanding node " + node);

            sbyte[][] fLookupTable = null; // [0] - agent number ,[1] - f change, value = 1 - exists successor, value = -1 not exists, value = 0 don't know

            Expand(node, 0, runner, new HashSet<TimedMove>(), allMoves, node.currentFChange, fLookupTable);
            node.currentFChange++;
            node.h++;
            while (node.hasMoreChildren(maxFchange) && existingChildForF(allMoves, 0, node.currentFChange) == false)
            {
                node.currentFChange++;
                node.h++;
            }

            if (node.hasMoreChildren(maxFchange) && existingChildForF(allMoves, 0, node.currentFChange) && node.h + node.g <= this.maxCost)
                openList.Add(node);
            return true;
        }

        protected bool Expand(WorldState currentNode, int agentIndex, Run runner, HashSet<TimedMove> currentMoves, byte[][] allMoves, int targetFchange, sbyte[][] fLookupTable)
        {
            if (existingChildForF(allMoves, agentIndex, targetFchange) == false)
                return false;

            if (targetFchange < 0)
                return false;
            if (runner.ElapsedMilliseconds() > Constants.MAX_TIME)
                return true;
            WorldStateForPartialExpansion prev = (WorldStateForPartialExpansion)currentNode.prevStep;
            if (agentIndex == 0) // If this is the first agent that moves
            {
                prev = (WorldStateForPartialExpansion)currentNode;
            }
            if (agentIndex == instance.m_vAgents.Length) // If all the agents have moved
            {
                if (targetFchange != 0)
                    return false;

                return ProcessGeneratedNode(currentNode);
            }

            // Try all legal moves of the agents
            CbsConstraint nextStepLocation = new CbsConstraint();
            WorldStateForPartialExpansion childNode;
            bool ans = false;
            foreach (TimedMove agentLocation in currentNode.allAgentsState[agentIndex].last_move.GetNextMoves(Constants.ALLOW_DIAGONAL_MOVE))
            {
                if (this.constraintList != null)
                {
                    nextStepLocation.init(instance.m_vAgents[agentIndex].agent.agentNum, agentLocation);
                    if (constraintList.Contains(nextStepLocation))
                        continue;
                }
                if (IsValid(agentLocation, currentMoves))
                {
                    currentMoves.Add(agentLocation);
                    childNode = new WorldStateForPartialExpansion(currentNode);
                    childNode.allAgentsState[agentIndex].move(agentLocation);
                    childNode.prevStep = prev;
                    if (Expand(childNode, agentIndex + 1, runner, currentMoves, allMoves, targetFchange - allMoves[agentIndex][(int)agentLocation.direction], fLookupTable))
                        ans = true;
                    currentMoves.Remove(agentLocation);
                }
            }
            return ans;
        }

        public bool existingChildForF(byte[][] allMoves, int agent, int targetFchange) 
        {
            // allMoves[][] = [0] - agent number [1] - direction [in table]- effecte on F)
            // fLookup[][] = [0] - agent number ,[1] - f change, value =1 - exists successor, value = -1 not exists, value = 0 dont know

            if (targetFchange < 0)
            {
                return false;
            }

            if (agent == allMoves.Length)
            {
                if (targetFchange == 0)
                    return true;
                return false;
            }

            if (fLookup == null)
            {
                fLookup = new sbyte[allMoves.Length][];
                for (int i = 0; i < fLookup.Length; i++)
                {
                    fLookup[i] = new sbyte[1 + 2 * fLookup.Length];
                }
            }

            if (targetFchange + 1 > fLookup[agent].Length)
            {
                sbyte[] old = fLookup[agent];
                fLookup[agent] = new sbyte[targetFchange + 1];
                for (int i = 0; i < old.Length; i++)
                {
                    fLookup[agent][i] = old[i];
                }
            }

            if (fLookup[agent][targetFchange] != 0)
            {
                return fLookup[agent][targetFchange] == 1; 
            }

            for (int i = 0; i < allMoves[agent].Length; i++)
            {
                if (allMoves[agent][i] > targetFchange)
                    continue;
                if (existingChildForF(allMoves, agent + 1, targetFchange - allMoves[agent][i]))
                {
                    fLookup[agent][targetFchange] = 1;
                    return true;
                }
            }
            fLookup[agent][targetFchange] = -1;
            return false;
        }
    }
}
