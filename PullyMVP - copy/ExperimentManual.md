# PulleyMVP Pilot Study – Session Manual

**For a single host running one session alone.** Lu and Knud each run sessions on their own equipment, using the same Unity project and this manual.

- Read every *quoted* instruction **word for word**, so all sessions are the same.
- `[TO ADD]` = material not finalised yet.
- Click a section title to expand it.

**Terms used with participants**

| Term | Meaning |
|---|---|
| **visual hints and guides** | The conceptual aid: force arrows, distance rulers, formula panel |
| **automatic setup** | The system places the equipment; the participant presses **Play** |
| **"Hints on, please"** | The fixed phrase group B uses to request the aid |

**Support levels (host only)**

| Level | Equipment setup | Visual hints |
|---|---|---|
| L2 | Participant places it | Off |
| L1 | Participant places it | On |
| L0 | Automatic | On |
| Auto | Automatic | Off (group B only) |

---

<details open>
<summary><b>1. Overview</b></summary>

| Step | Time | Group A | Group B | Group C |
|---|---|---|---|---|
| **Support during task** | – | Fixed at L2 | Participant asks out loud; host switches | AI switches L2 → L1 → L0 based on cognitive load (CL) |
| 1. Consent + background questionnaire | 3′ | Same | Same | Same |
| 2. Pre-test (paper) | 3′ | Same | Same | Same |
| 3. Headset on + calibration | 5′ | Same | Same | Same |
| 4. Practice P1 | A 2′ / B, C 3.5′ | Controls, hang weight, pull rope, answer | + try requesting hints and automatic setup | + host demonstrates L1 and L0 |
| 5. Practice P2 | 1′ | Place movable pulley, answer | Same | Same; **return control to CL** |
| 6. Mental effort #1 | 0.5′ | Same | Same | Same |
| 7. Task (6 questions) | 25–30′ | No panel actions | Press Hints / Auto when asked | Watch Status; override only if the classifier fails |
| 8. Mental effort #2, stop recording, headset off | 1′ | Same | Same | Same |
| 9. Questionnaires (SSQ, SUS, UES-SF) | 4′ | Same | Same | Same |
| 10. Post-test (paper) | 3′ | Same | Same | Same |
| 11. Per-question effort ratings | 2′ | Same | Same | Same |
| 12. Interview with replay | 5–8′ | Focus: when help was wanted | Focus: when and why help was requested | Focus: noticing changes, timing |
| **Total** | **≈ 52′** | | | |

**Controller**

| Action | Button |
|---|---|
| Grab weight or pulley | **Grip** (middle finger) |
| Pull rope | **Trigger** (index finger) on the free end of the rope |
| Select answer / button | **Right-hand** ray + **Trigger** |

</details>

---

<details>
<summary><b>2. Before each test day</b></summary>

**Project**
- [ ] Pull latest version from GitHub; confirm **same commit** as the other host. Do not edit the project during the test period.
- [ ] Open the experiment scene; Console shows **no red errors**
- [ ] `FidelityConfig`: boundary2to1 = 33.33, boundary1to0 = 66.67, buffer = 5
- [ ] `CognitiveLoadController` → **UseModel** ticked

**Equipment**
- [ ] Headset and controllers charged; PCVR streaming (VIVE Hub / SteamVR) works
- [ ] Eye tracking works (quick Play test, then stop)
- [ ] Headset cleaned; hygiene cover ready

**Recording**
- [ ] OBS captures the VR mirror window; enough disk space
- [ ] Phone audio recorder works
- [ ] Video player ready for replay (e.g. VLC)

**Paper materials** (one set per participant)
- [ ] Consent form, background questionnaire `[TO ADD]`
- [ ] Pre-test, post-test `[TO ADD]`
- [ ] SSQ / SUS / UES-SF combined questionnaire `[TO ADD]`
- [ ] Session sheet (Appendix B), per-question rating form (Appendix C), interview sheet (Appendix E)
- [ ] Mental effort scale card (Appendix D), printed and laminated
- [ ] Printed images of the 6 questions (1-1, 1-2, 2-1, 2-2, 3-1, 3-2) for Appendix C
- [ ] Access to the shared group assignment sheet (Appendix F)

</details>

---

<details>
<summary><b>3. Before each participant</b></summary>

1. In the **shared group sheet** (Appendix F), take the **next free row**. Note the **ID** and **group**, and add your name and the date.
2. In Unity (**do not press Play yet**):
   - `ExperimentLogger` → **participantId** = ID
   - `WizardControlPanel` → **Group** = A / B / C
3. Start **OBS recording** and **phone audio**. Say: *"Participant [ID], group [X], [date]."* **Never say the participant's name.**
4. Press **Play only when the participant is wearing the headset and ready to calibrate.** Calibration starts as soon as Play is pressed.
5. When you press Play, write the **OBS time** on the session sheet (Appendix B, "OBS offset"). You need it to find moments in the video later.

</details>

---

<details>
<summary><b>4. Step-by-step procedure</b></summary>

### Step 1 – Consent + background questionnaire (3′)
- Explain the study. Audio and screen are recorded. They can **stop at any time without giving a reason**.
- Consent form signed; background questionnaire completed.

### Step 2 – Pre-test (3′)
- Paper test. *"Please answer as well as you can. It's fine if you don't know."* `[TO ADD: test items]`

### Step 3 – Headset on + calibration (5′)
- Fit the headset and adjust it until the image is sharp.
- Press **Play** → note the OBS offset → calibrate. `[TO ADD: Knud's calibration steps and when to use Start / Stop baseline music]`

### Step 4 – Practice P1 (fixed pulley + one weight)

**All groups**

| Host says | Participant does |
|---|---|
| *"Use the grip button under your middle finger to pick up the weight, and hang it on the left hook."* | Hangs weight |
| *"Now point at the free end of the rope, hold the trigger under your index finger, and pull it down a little. Then let go."* | Pulls rope |

**Group B only** (the participant says it first, then you press)

| Host says | Participant says / does | Host presses |
|---|---|---|
| *"During the task you can ask for visual hints and guides, like arrows that show forces or rulers that show how far things move. Try it now: say 'Hints on, please'."* | *"Hints on, please"* | **Aid ON** |
| *"You can also ask the system to set up the equipment for you. Say 'Hints off, automatic setup on, please'."* | Says it | **Aid OFF**, then **Auto setup ON** |
| *"Now press Play."* | Presses Play; weight is placed automatically | – |
| *"You can ask for hints, automatic setup, or both, at any time, and turn them off again. It does not affect your results."* | – | **Auto setup OFF** |

**Group C only**

| Host says | Host presses |
|---|---|
| *"During the task, the system may show visual hints and guides, like these arrows."* | **L1** |
| *"The system may also set up the equipment for you. Please press Play."* | **L0** (participant presses Play) |
| *"The system decides on its own when to change these."* | **L2** |

**All groups**

| Host says | Participant does |
|---|---|
| *"Point your right hand at an answer, press the trigger to select it, then select Confirm."* | Answers (correct: **1**) |
| *"If you're ever unsure about an answer, you can test your idea with the equipment, or just try an answer. If it's wrong, you can try again."* | – |
| *"Now select Next to go to the next page."* | Pages forward |

### Step 5 – Practice P2 (fixed + movable pulley)

| Host says | Participant does |
|---|---|
| *"Use the grip button to pick up the movable pulley and place it on the rope. You don't need to pull anything."* | Places pulley |
| *"Now answer the question and select Confirm. Please don't press Next yet."* | Selects **Yes** → Confirm |

- **Group C:** press **Return to CL control**. Status must **not** show `MANUAL OVERRIDE`.

### Step 6 – Mental effort #1 (0.5′)
> *"I'd like you to rate how much mental effort the practice took. By mental effort I mean how much you had to think and concentrate. It's not about how difficult the task was or how well you did. On a scale from 1 to 9, where 1 is very, very low and 9 is very, very high, what would you say?"*

- If asked about physical effort: *"Only the thinking, not the physical part."*
- Write the score on the session sheet. Then:

> *"Thank you. That was the practice. The real questions start now. Take as much time as you need. Select Next when you're ready."*

### Step 7 – Task (25–30′)

**Panel actions**

| Group A | Group B | Group C |
|---|---|---|
| None | When the participant asks, press the matching button **immediately, without comment** | Watch Status; if the classifier fails, override (Section 6) |

- **Group B, unclear request:** *"Would you like the visual hints, the automatic setup, or both?"*
- **Any group, physics question:** *"I can't help with the physics, but you can test it with the equipment."*

**If the participant is stuck** (same for all groups; never give physics hints)

| Trigger | Host says | Log as |
|---|---|---|
| Says "I don't know", or no action for about **60 s** | *"You can try it out with the equipment."* | Prompt 1 |
| About **60 s** later, still stuck | *"You can also choose the answer you think is most likely. If it's wrong, you can try another one."* | Prompt 2 |
| After a correct answer reached by trial and error | *"Can you use the equipment to check why this answer is right?"* | Prompt 3 |
| More than **6 min** on one question | Jump to the next question in the panel | Skip |

**What to log** (OBS time + keyword; times, answers and level changes are logged automatically)
- Spoken comments: confusion, insight, complaints
- Group B: the exact wording of requests, and hesitations (wanted help but didn't ask)
- Your interventions: prompts 1–3, skips, technical help
- Strategy: tests with equipment, guesses, trial and error
- Problems: technical faults, overrides (and why), discomfort
- Mark ★ any moment worth replaying in the interview

### Step 8 – Mental effort #2, then end the VR part (1′)
When the Thank-you screen appears, **keep the headset on**:
> *"Using the same scale, how much mental effort did the whole task take?"*

Then, in this order:
1. Write the score on the session sheet.
2. **Stop OBS recording.** Keep phone audio running.
3. Remove the headset.
4. **Stop Play** in Unity. The TOTAL row is written at this point.

### Step 9 – Questionnaires (4′)
SSQ, SUS, UES-SF on one combined form `[TO ADD]`.
**Meanwhile:** select the replay moments (Section 5).

### Step 10 – Post-test (3′)
Paper; isomorphic to the pre-test `[TO ADD]`.

### Step 11 – Per-question effort ratings (2′)
Show the 6 printed question images one at a time, in task order, together with the scale card:
> *"Here are the questions you solved. For each one, please rate the mental effort it took, on the same 1-to-9 scale."*

Record on Appendix C.

### Step 12 – Interview (5–8′)
See Section 5. Afterwards, thank the participant, briefly explain the purpose of the study, and stop the audio recording.

</details>

---

<details>
<summary><b>5. Interview</b></summary>

### Choosing the two replay moments (during Step 9)

| | Moment ① – support moment | Moment ② – hardest moment |
|---|---|---|
| **Group A** | The longest stuck period within Moment ②'s question | `summary.csv`: row with the **most Attempts**; if tied, the longest **Duration** |
| **Group B** | **First** `LearnerRequest` in `events.csv`; a logged "wanted help but didn't ask" moment takes priority | Same |
| **Group C** | **First** `SupportChanged` with Source `CL` (usually L2 → L1) | Same |

- A ★ moment of the same kind takes priority.
- **OBS time = events Time + OBS offset.**
- Open the video and go to 10 s before each moment.

### At each moment
1. Play from 10 s before to 10 s after.
2. *"What were you thinking at this moment?"*
3. Group question:
   - A: *"Would help have been useful here? What kind of help?"*
   - B: *"Why did you ask here?"* / *"What made you not ask?"*
   - C: *"Did you notice the change? How did it feel?"*
4. *"How much mental effort was this moment, from 1 to 9?"*

### Core questions `[DRAFT – wording to be finalised]`

| # | All groups | Group A | Group B | Group C |
|---|---|---|---|---|
| 1 | *"How was the experience overall?"* | | | |
| 2 | *"Which parts were difficult, and why?"* | | | |
| 3 | | *"Was there a moment you wished for more help? What kind?"* | *"When did you ask for help, and why? Was there a moment you wanted help but didn't ask?"* | *"Did you notice the system changing? Did changes come at the right time? Did you feel in control?"* |
| 4 | | | *"Were the hints / automatic setup helpful? How?"* | *"Were the hints / automatic setup helpful? How?"* |
| 5 | *"What would you change?"* | | | |

</details>

---

<details>
<summary><b>6. Emergencies</b></summary>

| Situation | Action |
|---|---|
| **Participant feels unwell** | **Stop as soon as they say so.** Remove the headset; note the time and reason. Ask if they are willing to do the questionnaires and interview (no pressure). Mark the session "incomplete" in Appendix F. |
| **Classifier failure** (group C: CL frozen, at 0, or jumping) | Override with L2 / L1 / L0 in the panel; log the time and reason. When fixed, press **Return to CL control**. |
| **Tracking lost / stream disconnected** | Ask the participant to stand still; reconnect; continue. Log the time. |
| **Unity crashes** | 1. Keep the **same participant ID**. 2. **Before pressing Play again**, rename the 3 eye-tracking files (`participant_{ID}_calibration / _measurements / _predictions.csv`) by adding `_part1`; otherwise they are **overwritten**. 3. Press Play, recalibrate, jump to the interrupted question in the panel, and continue. 4. Log the time. Summary/events files are **not** overwritten; a new pair is created and the two are merged afterwards. |
| **Wrong ID or group entered** | Stop and correct it before the task. If the task has already started, log it and correct the file names afterwards. |

</details>

---

<details>
<summary><b>7. After the session</b></summary>

1. Check that `summary.csv` ends with a **TOTAL** row.
2. Copy the following into the shared data folder `[TO ADD: location]` → folder `P{ID}_group{X}`:
   - From `CognitiveLoadData`: `summary.csv`, `events.csv`, and the 3 eye-tracking files
   - OBS video and phone audio
   - Scans of the session sheet, rating form, interview sheet, questionnaires and tests
   - Keep consent forms **separately**; never with the data
3. Update Appendix F (status, notes).
4. Clean the headset.

</details>

---

## Appendices (printable / fillable)

<details>
<summary><b>Appendix A – Wizard panel quick reference</b></summary>

| Control | Group | Effect |
|---|---|---|
| Group dropdown | All | Sets the group. **Only editable before Play** |
| Start / Stop baseline music | All | Calibration music |
| P1 / P2 / question buttons | All | Jump to a question (emergency or skip only) |
| Aid ON / OFF | B | Visual hints on or off |
| Auto setup ON / OFF | B | Automatic setup on or off |
| L2 / L1 / L0 | B: shortcut · C: manual override | Sets the level directly |
| Return to CL control (yellow) | C | Ends the manual override |

Status shows the question, group, scene level, CL and shadow level. `Change queued` means the participant is holding something; the change is applied when they let go.

</details>

<details>
<summary><b>Appendix B – Session sheet</b></summary>

| Field | Entry |
|---|---|
| Participant ID | |
| Group (A / B / C) | |
| Host | |
| Date / start time | |
| **OBS offset** (OBS time when Play was pressed) | ___ : ___ |
| Mental effort #1 (after practice, 1–9) | |
| Mental effort #2 (after task, 1–9) | |
| Session completed? | ☐ Yes ☐ No – reason: |

**Setup check**
- [ ] ID set in ExperimentLogger
- [ ] Group set in WizardControlPanel
- [ ] OBS + audio recording started; ID / group spoken
- [ ] Group C: Return to CL control pressed after practice

**Observation log**

| OBS time | Question | Type (Comment / Request / Prompt 1–3 / Skip / Strategy / Problem) | Keyword | ★ |
|---|---|---|---|---|
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |

</details>

<details>
<summary><b>Appendix C – Per-question effort ratings</b></summary>

Participant ID: ______ Group: ______

*"How much mental effort did this question take?"* Tick one per row.

| Question | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|
| 1-1 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 1-2 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 2-1 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 2-2 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 3-1 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 3-2 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |

</details>

<details>
<summary><b>Appendix D – Mental effort scale card (print and show)</b></summary>

**How much mental effort did it take?**
*(How much you had to think and concentrate. Not how difficult it was or how well you did.)*

| Rating | Label |
|---|---|
| 1 | Very, very low mental effort |
| 2 | Very low mental effort |
| 3 | Low mental effort |
| 4 | Rather low mental effort |
| 5 | Neither low nor high mental effort |
| 6 | Rather high mental effort |
| 7 | High mental effort |
| 8 | Very high mental effort |
| 9 | Very, very high mental effort |

*Scale: Paas (1992).*

</details>

<details>
<summary><b>Appendix E – Interview sheet</b></summary>

Participant ID: ______ Group: ______

**Replay moments**

| | events Time | OBS time (+ offset) | Why chosen | Effort (1–9) | Key points said |
|---|---|---|---|---|---|
| ① Support moment | | | | | |
| ② Hardest moment | | | | | |

**Core questions**

| # | Key points |
|---|---|
| 1 Overall experience | |
| 2 Difficult parts | |
| 3 Group-specific | |
| 4 Hints / automatic setup (B, C) | |
| 5 What to change | |
| Other remarks | |

</details>

<details>
<summary><b>Appendix F – Shared group assignment sheet</b></summary>

Fill the Group column **in advance** with a shuffled list of 5 A, 5 B and 5 C. Take rows **in order**; never skip one.

| ID | Group | Host | Date | Status (complete / incomplete) | Notes |
|---|---|---|---|---|---|
| 1 | | | | | |
| 2 | | | | | |
| 3 | | | | | |
| 4 | | | | | |
| 5 | | | | | |
| 6 | | | | | |
| 7 | | | | | |
| 8 | | | | | |
| 9 | | | | | |
| 10 | | | | | |
| 11 | | | | | |
| 12 | | | | | |
| 13 | | | | | |
| 14 | | | | | |
| 15 | | | | | |

</details>