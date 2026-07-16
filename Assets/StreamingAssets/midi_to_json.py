import json
import sys
from mido import MidiFile

if len(sys.argv) < 2:
    print("Usage: python midi_to_json.py song.mid")
    exit()

mid = MidiFile(sys.argv[1])

notes = []

current_time = 0

for track in mid.tracks:
    current_time = 0

    for msg in track:
        current_time += msg.time

        if msg.type == "note_on" and msg.velocity > 0:
            notes.append({
                "note": msg.note,
                "velocity": msg.velocity,
                "time": current_time
            })

output = {
    "notes": notes
}

with open("song.json", "w") as f:
    json.dump(output, f, indent=4)

print("Saved song.json")