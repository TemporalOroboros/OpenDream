﻿using System;
using System.Collections.Generic;
using System.Drawing;

namespace OpenDreamShared.Dream {
    class DreamFullState {
        public struct Atom {
            public UInt16 AtomID;
            public UInt16 BaseID;
            public UInt16 LocationID;
            public IconVisualProperties VisualProperties;
            public ScreenLocation ScreenLocation;
            public Dictionary<UInt16, IconVisualProperties> Overlays;
        }

        public class Client {
            public UInt16 EyeID = 0xFFFF;
            public List<UInt16> ScreenObjects = new List<UInt16>();

            public Client CreateCopy() {
                return new Client() {
                    EyeID = this.EyeID,
                    ScreenObjects = this.ScreenObjects
                };
            }
        }

        public UInt32 ID;
        public Dictionary<UInt16, Atom> Atoms = new Dictionary<UInt16, Atom>();
        public Dictionary<string, Client> Clients = new Dictionary<string, Client>();
        public UInt16[,] Turfs = new UInt16[0, 0];

        public DreamFullState(UInt32 id) {
            ID = id;
        }

        public void SetFromFullState(DreamFullState fullState) {
            foreach (KeyValuePair<UInt16, Atom> atom in fullState.Atoms) {
                Atoms.Add(atom.Key, atom.Value);
            }

            foreach (KeyValuePair<string, Client> client in fullState.Clients) {
                Clients.Add(client.Key, client.Value.CreateCopy());
            }

            Turfs = fullState.Turfs;
        }

        public void ApplyDeltaStates(List<DreamDeltaState> deltaStates) {
            foreach (DreamDeltaState deltaState in deltaStates) {
                foreach (DreamDeltaState.AtomCreation atomCreation in deltaState.AtomCreations) {
                    Atom atom = new Atom();

                    atom.AtomID = atomCreation.AtomID;
                    atom.BaseID = atomCreation.BaseID;
                    atom.LocationID = atomCreation.LocationID;
                    atom.VisualProperties = atomCreation.VisualProperties;
                    atom.ScreenLocation = atomCreation.ScreenLocation;
                    atom.Overlays = new Dictionary<ushort, IconVisualProperties>();

                    Atoms[atom.AtomID] = atom;
                }

                foreach (DreamDeltaState.AtomLocationDelta atomLocationDelta in deltaState.AtomLocationDeltas) {
                    Atom atom = Atoms[atomLocationDelta.AtomID];

                    atom.LocationID = atomLocationDelta.LocationID;
                    Atoms[atomLocationDelta.AtomID] = atom;
                }

                foreach (UInt16 atomDeletion in deltaState.AtomDeletions) {
                    Atoms.Remove(atomDeletion);
                }

                foreach (DreamDeltaState.AtomDelta atomDelta in deltaState.AtomDeltas) {
                    Atom atom = Atoms[atomDelta.AtomID];

                    if (atomDelta.ChangedVisualProperties.HasValue) {
                        atom.VisualProperties = atom.VisualProperties.Merge(atomDelta.ChangedVisualProperties.Value);
                    }

                    if (atomDelta.ScreenLocation.HasValue) {
                        atom.ScreenLocation = atomDelta.ScreenLocation.Value;
                    }

                    if (atomDelta.OverlayRemovals.Count > 0) {
                        foreach (UInt16 overlayID in atomDelta.OverlayRemovals) {
                            atom.Overlays.Remove(overlayID);
                        }
                    }

                    if (atomDelta.OverlayAdditions.Count > 0) {
                        foreach (KeyValuePair<UInt16, IconVisualProperties> overlay in atomDelta.OverlayAdditions) {
                            atom.Overlays.Add(overlay.Key, overlay.Value);
                        }
                    }

                    Atoms[atomDelta.AtomID] = atom;
                }

                foreach (DreamDeltaState.TurfDelta turfDelta in deltaState.TurfDeltas) {
                    Turfs[turfDelta.X, turfDelta.Y] = turfDelta.TurfAtomID;
                }

                foreach (KeyValuePair<string, DreamDeltaState.ClientDelta> clientDelta in deltaState.ClientDeltas) {
                    Client client;
                    if (!Clients.TryGetValue(clientDelta.Key, out client)) {
                        client = new Client();

                        Clients[clientDelta.Key] = client;
                    }

                    if (clientDelta.Value.NewEyeID.HasValue) {
                        client.EyeID = clientDelta.Value.NewEyeID.Value;
                    }

                    if (clientDelta.Value.ScreenObjectAdditions != null) {
                        foreach (UInt16 screenObjectID in clientDelta.Value.ScreenObjectAdditions) {
                            client.ScreenObjects.Add(screenObjectID);
                        }
                    }

                    if (clientDelta.Value.ScreenObjectRemovals != null) {
                        foreach (UInt16 screenObjectID in clientDelta.Value.ScreenObjectRemovals) {
                            client.ScreenObjects.Remove(screenObjectID);
                        }
                    }
                }
            }
        }
    }
}
