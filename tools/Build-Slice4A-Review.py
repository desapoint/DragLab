"""Source-backed 4A inventory. Does not import Unity or modify game files."""
from pathlib import Path
from collections import Counter
import argparse
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs/ui/4a"
RUNTIME = ROOT / "Game/Assets/DragLab/Scripts/Runtime"
DOMAIN = ROOT / "Game/Assets/DragLab/Scripts/Domain"
CALL = re.compile(r"(?<![\w.])(?:(?:GUILayout|GUI)\.(?:Button|RepeatButton|Toggle|HorizontalSlider|VerticalSlider|TextField|TextArea|SelectionGrid|Toolbar|Label|Box)|(?:\w*Button|\w*Slider|Equipment|Limiter|Cut|EquipmentButtons|Number|FluidQuantity)|TuningSlider\.Draw|fields\.Draw(?:Array)?)\s*\(")
METHOD = re.compile(r"(?m)^\s*(?:(?:public|private|internal|protected|static|sealed|override|virtual|async)\s+)*(?:[\w.<>\[\],]+)\s+(\w+)\s*\([^;{}]*\)\s*(?:\{|=>)")
BASE = {
    'PrototypeGame': ('4B', 'shell'), 'CollectionGarage': ('4D', 'cars'),
    'CarStatsView': ('4D/4E/4H', 'stats'), 'CarStatsSummary': ('4D/4E/4H', 'stats'),
    'CarStatsOverlay': ('4D/4E/4H', 'stats'), 'TuningGarage': ('4E', 'tuning'),
    'EquipmentTuningView': ('4E/4N', 'equipment'), 'AwdSettingsView': ('4E/4N', 'awd'),
    'PartsGarage': ('4F/4G/4H', 'workshop'), 'PartsCartGarage': ('4H', 'shop'),
    'ComponentMaintenance': ('4F', 'maintenance'), 'PaidOperationsGarage': ('4F/4I/4K', 'payments'),
    'FluidServiceView': ('4F/4N', 'fluids'), 'IndividualEngineView': ('4F/4N', 'individual'),
    'AssemblySetupGarage': ('4I', 'assemblies'), 'ValuationGarage': ('4I', 'valuation'),
    'GarageBusinessView': ('4K', 'business'), 'EconomyRecoveryView': ('4K', 'recovery'),
    'AudioSettingsView': ('4C/4M/4N', 'audio'), 'UnitSettingsView': ('4C/4M/4N', 'units'),
    'SimulationSettingsView': ('4C/4N', 'difficulty'), 'GameReset': ('4C', 'reset'),
    'EngineTestScene': ('4M', 'sound'), 'EngineBenchDynoView': ('4M', 'sound-graphs'),
    'BenchFieldControls': ('4B/4M', 'reflected-widget'), 'PhysicalDrivingBench': ('4N', 'physical'),
    'PhysicalBenchMass': ('4N', 'mass'), 'FuelView': ('4F/4M/4N', 'fuel'),
    'DrivingTraceView': ('4N', 'physical-graphs'), 'TuningSlider': ('4B/4E/4N', 'slider'),
}
TYPE_MAP = {
    'config.vehicle': 'VehicleSpec', 'config.vehicle.flywheel': 'FlywheelSpec',
    'config.vehicle.engineBehavior': 'EngineBehaviorSpec', 'config.vehicle.induction': 'InductionSpec',
    'config.vehicle.transmission': 'TransmissionSpec', 'config.vehicle.wheels': 'WheelSpec',
    'config.vehicle.sound': 'EngineSoundSpec', 'config.tune': 'Tune',
    'config.controls': 'EngineBenchControls', 'displayed': 'EngineBenchControls',
    'setup.parts[i]': 'ExhaustPart',
}

def mask(text):
    """Mask comments and literals, retaining offsets/newlines for source locations."""
    p = re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'')
    return p.sub(lambda m: ''.join('\n' if c == '\n' else ' ' for c in m.group()), text)

def closing(masked, start, left='(', right=')'):
    depth = 0
    for i in range(start, len(masked)):
        if masked[i] == left: depth += 1
        elif masked[i] == right:
            depth -= 1
            if depth == 0: return i
    raise ValueError('Unbalanced source delimiter')

def context(file, method, offset, text):
    phase, area = BASE[file]
    # Shared partial-class files contain both player and physical-bench UI.
    if file in {'FluidServiceView', 'IndividualEngineView', 'FuelView'}:
        classes = list(re.finditer(r'class\s+(PrototypeGame|EngineTestScene|PhysicalDrivingBench)', text[:offset]))
        owner = classes[-1].group(1) if classes else ''
        if owner == 'PhysicalDrivingBench': return '4N', 'physical'
        if owner == 'EngineTestScene': return '4M', 'sound'
        return '4F', 'maintenance'
    if file == 'PrototypeGame':
        if method in {'GarageGUI', 'UpgradeButton'}: return '4D/4F', 'garage'
        if method in {'CareerGUI', 'FreeRaceGUI', 'DifficultyGUI'}: return '4J', 'races'
        if method in {'DrivingGUI', 'SettingsGUI'}: return '4C', 'settings'
        if method in {'RaceGUI', 'ResultGUI'}: return '4L', 'race'
    if file == 'AssemblySetupGarage' and method == 'SetupGUI': return '4E', 'setups'
    if file == 'PartsCartGarage' and ('Inventory' in method or 'Move' in method): return '4G/4H', 'inventory'
    if file == 'PartsGarage' and method == 'RepairsGUI': return '4F', 'maintenance'
    if file == 'PaidOperationsGarage':
        if method == 'DrawRecovery': return '4K', 'recovery'
        if method == 'QuoteSaleGUI': return '4I', 'valuation'
    return phase, area

def semantics(area, api):
    if api.endswith(('Label', 'Box')):
        return {'action': 'read-only feedback; retain the expression and its units', 'owner': 'observation only', 'persistence': 'none', 'permission': 'display existing state'}
    if area in {'sound', 'sound-graphs', 'physical', 'physical-graphs', 'mass', 'reflected-widget'}:
        return {'action': 'isolated test edit/session/scenario/export; see source expression', 'owner': 'bench; independent config preserves drive ownership', 'persistence': 'test files only on explicit export; no player assets', 'permission': 'existing fixture/physical-run and pause gates; hardware protections retained'}
    if area in {'settings', 'audio', 'units', 'difficulty', 'reset'}:
        return {'action': 'preference/display/reset action; see source expression', 'owner': 'preferences; does not claim driving inputs', 'persistence': 'preference gesture completion; reset confirmation preserves preferences', 'permission': 'thermal dependencies and explicit reset confirmation'}
    if area in {'tuning', 'equipment', 'awd', 'setups', 'slider'}:
        return {'action': 'draft adjustment or explicit configuration command; see source expression', 'owner': 'player draft or copied bench tune', 'persistence': 'only explicit Apply/save; navigation discards unapplied player draft', 'permission': 'installed hardware, supported ranges and active-run/reservation gates'}
    if area in {'workshop', 'shop', 'inventory', 'maintenance', 'payments', 'assemblies', 'valuation', 'recovery', 'business', 'cars', 'garage', 'fuel'}:
        return {'action': 'preview/selection or existing ownership/payment command; see source expression', 'owner': 'selected exact player instance; isolated copies for previews', 'persistence': 'selection/preview/staging is transient; definitive command saves atomically', 'permission': 'existing compatibility, identity, reservation, quote and affordability guards'}
    if area in {'race', 'races'}:
        return {'action': 'race selection/live input/pause/result action; see source expression', 'owner': 'race; held pedals independent from shift edges', 'persistence': 'existing entry/result/lifecycle saves and in-run journal', 'permission': 'phase, eligibility, hardware and pause rules'}
    return {'action': 'navigation/inspection; see source expression', 'owner': 'presentation', 'persistence': 'none for presentation; routed command retains its contract', 'permission': 'existing route/state gates'}

def domain_fields():
    result, enums = {}, set()
    for path in sorted(DOMAIN.rglob('*.cs')):
        text = path.read_text(encoding='utf-8-sig'); masked = mask(text)
        enums.update(re.findall(r'\benum\s+(\w+)', masked))
        for m in re.finditer(r'\b(?:class|struct)\s+(\w+)[^{]*\{', masked):
            start = m.end()-1; end = closing(masked, start, '{', '}')
            body = masked[start+1:end]; fields = []
            for f in re.finditer(r'\bpublic\s+(?:(readonly|static|const)\s+)?([\w.]+(?:\[\])?)\s+(\w+)(?=\s*[,=;])', body):
                # Only fields directly in this type, not nested types/methods.
                before = body[:f.start()]
                if before.count('{') != before.count('}') or f.group(1): continue
                absolute = start+1+f.start(); raw = text[absolute:start+1+body.index(';', f.end())+1]
                if '(' in raw.split('=', 1)[0]: continue
                names = [f.group(3)]
                # Multiple simple declarators (common in authored specs).
                tail = raw[raw.find(f.group(3))+len(f.group(3)):]
                names += re.findall(r',\s*(\w+)\s*(?==|,|;)', tail)
                for name in names:
                    fields.append({'name': name, 'type': f.group(2), 'source': path.relative_to(ROOT).as_posix(), 'line': text.count('\n', 0, absolute)+1})
            result[m.group(1)] = fields
    return result, enums

def inventory():
    records, sources, reflect = [], {}, []
    field_types, enums = domain_fields()
    for path in sorted(RUNTIME.glob('*.cs')):
        if any(s in path.stem for s in ['Verification', 'Probe']): continue
        text = path.read_text(encoding='utf-8-sig'); masked = mask(text)
        matches = list(CALL.finditer(text))
        # Ignore call-like strings/comments using the masked identifier.
        matches = [m for m in matches if masked[m.start():m.start()+3].strip()]
        if matches and path.stem not in BASE: raise ValueError('Unmapped UI source: '+path.name)
        methods = list(METHOD.finditer(masked))
        if matches or path.stem == 'RaceInputAdapter':
            sources[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
        for m in matches:
            start = text.index('(', m.start(), m.end()); end = closing(masked, start)
            # Exclude helper method declarations; include helper invocations.
            if any(d.start() <= m.start() < d.end() and d.group(1) == m.group().split('(')[0].strip() for d in methods): continue
            preceding = [d for d in methods if d.start() < m.start()]
            method = preceding[-1].group(1) if preceding else 'type-init'
            api = m.group().split('(')[0].strip(); expression = text[m.start():end+1]
            phase, area = context(path.stem, method, m.start(), text)
            line = text.count('\n', 0, m.start())+1
            key = hashlib.sha256(expression.encode()).hexdigest()[:8]
            record = {'id': f'{path.stem}.{method}.{line}.{key}', 'kind': 'read-only' if api.endswith(('Label', 'Box')) else 'interactive-template', 'source': path.relative_to(ROOT).as_posix(), 'line': line, 'method': method, 'api': api, 'expression': expression, 'destination': area, 'phase': phase, 'review': 'pending', **semantics(area, api)}
            records.append(record)
            if api == 'fields.Draw':
                args = text[start+1:end]; target = args.split(',', 1)[0].strip()
                if target not in TYPE_MAP: raise ValueError('Unmapped reflected target: '+target)
                literals = re.findall(r'"([^"\\]*)"', args)
                prefix = literals[0]; skip = set(literals[1:]); typ = TYPE_MAP[target]
                for field in field_types[typ]:
                    name, field_type = field['name'], field['type']
                    if name == 'version' or name in skip or (typ == 'Tune' and name.startswith('part')): continue
                    if field_type.split('.')[-1] not in enums | {'bool', 'int', 'float', 'double', 'float[]', 'TorquePoint[]'}: continue
                    reflect.append({'id': f'{record["id"]}.{prefix}.{name}', 'key': f'{prefix}.{name}', 'type': field_type, 'declaringType': typ, 'callSite': record['id'], **field, 'phase': phase, 'destination': area, 'owner': 'isolated bench; drive-field ownership only where applicable', 'persistence': 'no player save; explicit test capture/export only', 'permission': 'existing skip/keyboard/fixture validation rules', 'review': 'pending'})
    keys = []
    for path in sorted(RUNTIME.glob('*.cs')):
        if path.stem not in {'PrototypeGame', 'RaceInputAdapter', 'EngineTestScene', 'PhysicalDrivingBench'}: continue
        text = path.read_text(encoding='utf-8-sig')
        for line, value in enumerate(text.splitlines(), 1):
            codes = sorted(set(re.findall(r'KeyCode\.(\w+)', value)))
            if codes:
                keys.append({'source': path.relative_to(ROOT).as_posix(), 'line': line, 'keys': codes, 'expression': value.strip(), 'phase': '4L/4O/4P' if path.stem in {'PrototypeGame','RaceInputAdapter'} else '4M' if path.stem == 'EngineTestScene' else '4N', 'owner': 'existing route/focus/input-owner rules', 'persistence': 'input only except routed explicit commands', 'review': 'pending'})
    return {'format': 1, 'baseline': '1fe619b + runtime e5eed86', 'scope': 'static source call sites, reflected editable field templates, keyboard occurrences; dynamic item counts are not unique controls; UI binding parity still requires runtime checks', 'sources': sources, 'controls': records, 'reflectedFields': reflect, 'keyboard': keys}

def summary(data):
    counts = Counter((r['phase'], r['destination']) for r in data['controls'])
    lines = ['# 4A source control index', '', 'Generated by `tools/Build-Slice4A-Review.py`. Full expressions, source locations, context contracts, keyboard occurrences and reflected editable field paths are in [control-inventory.json](control-inventory.json).', '', 'Counts are source templates, not distinct rendered buttons or proof of runtime parity. Loop-generated catalog/items/positions/scenarios are retained as templates. Read-only feedback is included. Reflection expansion follows the current public-field editor and its skip lists; torque-point/ratio arrays remain indexed templates.', '', '| Destination phase | Area | Source call sites |', '|---|---|---|']
    lines += [f'| {p} | {a} | {n} |' for (p,a),n in sorted(counts.items())]
    lines += ['', f'**{len(data["controls"])} call sites**, **{len(data["reflectedFields"])} reflected field bindings**, **{len(data["keyboard"])} keyboard source occurrences**, **{len(data["sources"])} runtime UI/input source files**.', '', '## Source files', '']
    for name in data['sources']:
        lines.append(f'- [{Path(name).name}](../../../{name})')
    lines += ['', '## Reflected editable fields', '', '| Field path | Type | Source type | Phase |', '|---|---|---|---|']
    lines += [f'| `{r["key"]}` | {r["type"]} | {r["declaringType"]} | {r["phase"]} |' for r in data['reflectedFields']]
    return '\n'.join(lines)+'\n'

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true'); args = parser.parse_args()
    data = inventory(); content = json.dumps(data, ensure_ascii=False, indent=2)+'\n'; md = summary(data)
    OUT.mkdir(parents=True, exist_ok=True)
    browser_rows = [[r['phase'], Path(r['source']).name, r['line'], r['expression']] for r in data['controls']]
    browser_rows += [[r['phase'], r['declaringType'], r['line'], r['key']+' : '+r['type']] for r in data['reflectedFields']]
    browser_rows += [[r['phase'], Path(r['source']).name, r['line'], 'Keyboard: '+', '.join(r['keys'])] for r in data['keyboard']]
    browser_data = 'window.DragLab4AControls = '+json.dumps(browser_rows, ensure_ascii=False, separators=(',', ':'))+';\n'
    for name, value in [('control-inventory.json', content), ('SOURCE_INDEX.md', md), ('control-index.js', browser_data)]:
        path = OUT/name
        if args.check: assert path.read_text(encoding='utf-8-sig') == value, f'Stale inventory: {name}'
        else: path.write_text(value, encoding='utf-8')
    print(f'PASS: {len(data["controls"])} call sites / {len(data["reflectedFields"])} reflected bindings / {len(data["keyboard"])} keyboard occurrences / {len(data["sources"])} UI/input files; all mapped.')

if __name__ == '__main__': main()
