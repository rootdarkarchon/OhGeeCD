# Oh gee, CD

OGCD assistant plugin for Dalamud.

This plugin serves the purpose of tracking your OGCD during gameplay. It features audio output as well as visualization.

## Features

* Supports all combat classes (except BLU, for now)
* Supports all OGCD (Abilities)
* Supports all client languages
* OGCD Notifications
  * Early callouts: set a time before the OGCD comes off cooldown for the audio to play
  * Audio notification: Text-to-Speech
    * Allows a custom string to be spoken through Text-to-Speech 
    * Text-to-Speech is limited to Windows only
    * Text-to-Speech language can be freely set based on what you have installed on your operating system
  * Audio notification: Sound Effects
    * Allows the typical SFX from the game to be played on cooldown
    * Custom audio support: put in any mp3/wav/ogg and have it play
  * Visual notification: OGCD Bars
    * Configure unlimited amount of OGCD Groups of which each can have a separate bar
    * Each ability can be linked to one bar
    * Bars are fully customizeable in size, position, layout etc. 
    * All OGCDs placed on a bar and visualized will show their cooldowns and animation for the icon
    * OGCDs can be visualized or hidden based on condition (charges available, only on cooldown)
  * Visual notification: OGCD Tracker
    * Tracker with two modes for OGCD groups
      * Single-Line mode: all OGCD move from left to right on a single line
      * Multi-Group mode: all OGCD groups will get their own line
    * Each OGCD that is visualized in a group can also be drawn onto the tracker
* Adapts to your level: abilities that are not available are not displayed and not notified for
* Built future proof
  * all data is read from the game data and is resilent against changes
  * class changes in patches will be loaded and adjusted in seamlessly

## Installation

- Add following link to your Dalamud [Repository URL](https://darkarchon.internet-box.ch:8443/plogon/plogonmaster.json)
- Install Oh Gee, CD from the Available Plugins
