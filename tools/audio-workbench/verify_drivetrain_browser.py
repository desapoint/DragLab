from pathlib import Path
from playwright.sync_api import sync_playwright
root=Path('/mnt/data')
with sync_playwright() as play:
 b=play.chromium.launch(headless=True,executable_path='/usr/bin/chromium',args=['--no-sandbox','--disable-dev-shm-usage','--disable-gpu','--autoplay-policy=no-user-gesture-required'])
 page=b.new_page(accept_downloads=True)
 errors=[]
 page.on('pageerror',lambda e:errors.append(str(e)))
 page.set_content((root/'DragLab_Audio_Workbench_DrivetrainFix.html').read_text(),wait_until='domcontentloaded',timeout=20000)
 assert page.locator('#releaseThrottle').count()==1
 assert page.locator('#field-forwardGears').count()==1
 assert page.locator('#field-shiftStrategy').count()==1
 assert page.locator('#pedalMeter').count()==1
 page.locator('#preset').select_option('v8')
 assert page.locator('#field-forwardGears').input_value()=='6'
 page.locator('#throttle').fill('77')
 assert page.locator('#throttleLabel').inner_text()=='77%'
 page.locator('#releaseThrottle').click()
 assert page.locator('#throttleLabel').inner_text()=='0%'
 assert page.locator('#throttle').input_value()=='0'
 page.locator('#preset').select_option('tempest')
 page.locator('summary',has_text='Race & controls').click()
 assert page.locator('#field-shiftStrategy').count()==1
 # generated option select is a browser control and should export shift parameters.
 page.locator('#field-shiftStrategy').select_option('ThrottleLift')
 page.locator('#field-forwardGears').fill('6')
 with page.expect_download(timeout=9000) as dl:page.locator('#exportJson').click()
 import json
 file=root/'draglab-browser-export-drivetrain.json'
 dl.value.save_as(file)
 settings=json.loads(file.read_text())['settings']
 assert settings['forwardGears']==6 and settings['shiftStrategy']=='ThrottleLift',settings
 # View should start even with no local server (ScriptProcessor fallback).
 page.locator('#power').click(timeout=10000)
 page.wait_for_timeout(600)
 assert 'Pause' in page.locator('#power').inner_text()
 page.locator('#raceMode').select_option('Race')
 page.locator('#throttle').fill('90')
 page.wait_for_timeout(300)
 assert int(page.locator('#pedalMeter').inner_text().strip('%'))>20
 page.locator('#releaseThrottle').click()
 page.wait_for_timeout(500)
 assert int(page.locator('#pedalMeter').inner_text().strip('%'))<=5
 # Scenario output should be labeled as script and manual inputs cancel it.
 page.locator('#autoRun').click()
 page.wait_for_timeout(550)
 assert page.locator('#scriptMeter').inner_text()=='script',page.locator('#scriptMeter').inner_text()
 page.locator('#releaseThrottle').click()
 page.wait_for_timeout(200)
 assert page.locator('#scriptMeter').inner_text()=='manual'
 assert not errors,errors
 print('PASS: HTML UI forward gear controls, shift strategy, pedal release, JSON export, live telemetry, manual script takeover, audio start; no page errors')
 b.close()
