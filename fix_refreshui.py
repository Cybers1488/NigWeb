# -*- coding: utf-8 -*-
with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'r', encoding='utf-8') as f:
    content = f.read()
    
orig = """            var status = StepStatus.Incomplete;
            if (next == null)
                status = StepStatus.Complete;
            else if (next.Value.Step < 0 && i > -next.Value.Step - 1)
                status = StepStatus.Complete;
            else if (next.Value.Step < 0 && i <= -next.Value.Step - 1)
                status = StepStatus.Next;
            else if (next.Value.Surgery.Owner != _surgery.Value.Ent)
                status = StepStatus.Incomplete;
            else if (next.Value.Step == i)
                status = StepStatus.Next;
            else if (i < next.Value.Step)
                status = StepStatus.Complete;"""
                
new_logic = """            var status = StepStatus.Incomplete;
            var surgeryComp = _entities.GetComponent<SurgeryComponent>(_surgery.Value.Ent);
            
            if (!surgeryComp.RequireSequential)
            {
                if (_system.IsStepComplete(Owner, _part.Value, stepButton.Step, _surgery.Value.Ent))
                    status = StepStatus.Complete;
                else
                    status = StepStatus.Next;
            }
            else
            {
                if (next == null)
                    status = StepStatus.Complete;
                else if (next.Value.Step < 0 && i > -next.Value.Step - 1)
                    status = StepStatus.Complete;
                else if (next.Value.Step < 0 && i <= -next.Value.Step - 1)
                    status = StepStatus.Next;
                else if (next.Value.Surgery.Owner != _surgery.Value.Ent)
                    status = StepStatus.Incomplete;
                else if (next.Value.Step == i)
                    status = StepStatus.Next;
                else if (i < next.Value.Step)
                    status = StepStatus.Complete;
            }"""

if orig in content:
    content = content.replace(orig, new_logic)
    with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'w', encoding='utf-8') as f:
        f.write(content)
    print("Replaced!")
else:
    print("Original not found!")
