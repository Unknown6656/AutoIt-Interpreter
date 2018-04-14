; shamelessly copied from https://raw.githubusercontent.com/scimusmn/autoit-powerpoint/master/Powerpoint.au3

#include-once
#include <APIComConstants.au3>
#include 'header-1.au3'

#cs

        Functions - Create and Edit
		----------------------
		_PPT_PowerPointApp()
		_PPT_PowerPointQuit()
		_PPT_CreatePresentation()
		_PPT_PresentationAdd()
		_PPT_PresentationOpen()
		_PPT_PresentationSaved()
		_PPT_PresentationSaveAs()
		_PPT_PresentationName()
		_PPT_PresentationClose()
		_PPT_SlideCreate()
		_PPT_SlideTextFrameSetText()
		_PPT_SlideTextFrameGetText()
		_PPT_SlideTextFrameSetFont()
		_PPT_SlideTextFrameSetFontSize()
		_PPT_SlideCount()
		_PPT_SlideShapeCount()
		_PPT_SlideAddPicture()
		_PPT_SlideAddTable()
		_PPT_SlideAddTextBox()

        Functions - Slide Show Config Settings
		----------------------
		_PPT_SlideShowStartingSlide()
		_PPT_SlideShowEndingSlide()
		_PPT_SlideShowWithAnimation()
		_PPT_SlideShowWithNarration()
		_PPT_SlideShowLoopUntilStopped()
		_PPT_SlideShowAdvanceMode()
		_PPT_SlideShowShowType()
		_PPT_SlideShowRangeType()
		_PPT_SlideShowRun()
		_PPT_SlideShowAdvanceOnTime()
		_PPT_SlideShowAdvanceTime()

		Functions - Misc
		----------------------
		_PPT_bAssistant()
		_PPT_SlideSelect()

#ce

#cs -------------------------------------------------
PowerPoint constants
#ce ---------------------------------------------------

; _PPT_SlideCreate() layout types
Global Const $PPLAYOUTTITLE = 1
Global Const $PPLAYOUTTEXT = 2
Global Const $PPLAYOUTBLANK = 12
Global Const $PPLAYOUTCHART = 8
Global Const $PPLAYOUTCHARTANDTEXT = 6
Global Const $PPLAYOUTCLIPARTANDTEXT = 10
Global Const $PPLAYOUTCLIPARTANDVERTICALTEXT = 26
Global Const $PPLAYOUTTITLEONLY = 11

; _PPT_SlideShowAdvanceMode() modes
Global Const $PPMANUALADVANCE = 1
Global Const $PPUSETIMINGS = 2
Global Const $PPREHEARSENEWTIMINGS = 3

; _PPT_SlideShowShowType() types
Global Const $PPSHOWSPEAKER = 1 ;Only use speaker
Global Const $PPRSHOWWINDOW = 2 ;Display window around slides
Global Const $PPSHOWKIOSK = 3 ;Display full screen

; _PPT_SlideShowRangeType() types
Global Const $PPRANGETYPESHOWALL = 1 ; Show all slides
Global Const $PPRANGETYPESHOWBYRANGE = 2 ;Show by using start and ending range
Global Const $PPRANGETYPESHOWNAMEDSLIDESHOW = 3 ;Show named slides

#comments-start -------------------------------------------------
PowerPoint function implimentations
#comments-end ---------------------------------------------------

;===============================================================================
;
; Function Name:    _PPT_SlideTextFrameSetText()
; Description:      Sets the text of a specified TextFrame in a slide
; Parameter(s):     $obj - Slide object
;                   $intTextFrame - Index of TextFrame on slide
;					$Text - String text
; Return Value(s):  On Success - Slide's text frame text set
;                   On Failure - @error = 1, Returns 0
; Author(s):        Toady (Josh Bolton)
;
;===============================================================================
Func _PPT_SlideTextFrameSetText(ByRef $obj, $intTextFrame, $Text)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.Shapes.Item($intTextFrame).TextFrame.TextRange.Text = $Text
	Endif
EndFunc
;===============================================================================
;
; Function Name:    _PPT_SlideTextFrameTextSetFont()
; Description:      Sets the font face
; Parameter(s):     $obj - Slide object
;                   $intTextFrame - Index of TextFrame on slide
;					$font - String text ex. "Comic Sans MS"
; Return Value(s):  On Success - Slide's text frame text set
;                   On Failure - @error = 1, Returns 0
; Author(s):        Toady (Josh Bolton)
;
;===============================================================================
Func _PPT_SlideTextFrameSetFont(ByRef $obj, $intTextFrame, $font)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		Local $objTextRng = $obj.Shapes.Item($intTextFrame).TextFrame.TextRange
		$objTextRng.Font.Name = $font
	Endif
EndFunc
;===============================================================================
;
; Function Name:    _PPT_SlideTextFrameTextSetFontSize()
; Description:      Sets the text size
; Parameter(s):     $obj - Slide object
;                   $intTextFrame - Index of TextFrame on slide
;					$size - Integer value (ex. 24 or 36)
; Return Value(s):  On Success - Slide's text frame text set
;                   On Failure - @error = 1, Returns 0
; Author(s):        Toady (Josh Bolton)
;
;===============================================================================
Func _PPT_SlideTextFrameSetFontSize(ByRef $obj, $intTextFrame, $size)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		Local $objTextRng = $obj.Shapes.Item($intTextFrame).TextFrame.TextRange
		$objTextRng.Font.Size = $size
	Endif
EndFunc
;===============================================================================
;
; Function Name:    _PPT_SlideSelect()
; Description:      Selects a slide within PowerPoint
; Parameter(s):     $obj - Presentation object
; Return Value(s):  On Success - Slide's text frame text set
;                   On Failure - @error = 1, Returns 0
; Author(s):        Toady (Josh Bolton)
;
;===============================================================================
Func _PPT_SlideSelect(ByRef $obj)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.Select()
	Endif
EndFunc
;===============================================================================
;
; Function Name:    _PPT_SlideShowRun()
; Description:      Runs the slide show for presentation, same as pressing F5 in PPT
; Parameter(s):     $obj - Presentation object
; Return Value(s):  On Success - Presentation is ran
;                   On Failure - @error = 1, Returns 0
; Author(s):        Toady (Josh Bolton)
;
;===============================================================================
Func _PPT_SlideShowRun(ByRef $obj)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.SlideShowSettings.Run()
	Endif
EndFunc
;===============================================================================
;
; Function Name:    _PPT_SlideShowShowType()
; Description:      Sets the type of slide show type for presentation
; Parameter(s):     $obj - Presentation oject
;                   $intType - Either be 1, 2, or 3.. See constants above for types
; Return Value(s):  On Success - Slide show type is modified
;                   On Failure - @error = 1, Returns 0
;                              - @error = 2, Returns 2, Invalid type mode
; Author(s):        Toady (Josh Bolton)
;
;===============================================================================
Func _PPT_SlideShowShowType(ByRef $obj, $intType)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	ElseIf $intType > 3 Or $intType < 1 Then
		SetError(2)
		Return 2
	Else
		$obj.SlideShowSettings.ShowType = $intType
	Endif
EndFunc
;===============================================================================
;
; Function Name:    _PPT_SlideShowRangeType()
; Description:      Sets the range of slides that get displayed during slide show
; Parameter(s):     $obj - Presentation oject
;                   $intType - Either be 1, 2, or 3.. See constants above for types
; Return Value(s):  On Success - Slide show ranges are modified
;                   On Failure - @error = 1, Returns 0
;                              - @error = 2, Returns 2, Invalid type mode
; Author(s):        Toady (Josh Bolton)
;
;===============================================================================
Func _PPT_SlideShowRangeType(ByRef $obj, $intType)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	ElseIf $intType > 3 Or $intType < 1 Then
		SetError(2)
		Return 2
	Else
		$obj.SlideShowSettings.RangeType = $intType
	Endif
EndFunc
;===============================================================================
;
; Function Name:    _PPT_SlideShowAdvanceTime()
; Description:      Sets the number of each seconds each slide gets displayed globally
; Parameter(s):     $obj - Presentation oject
;                   $intType - Integer, number of seconds
; Return Value(s):  On Success - Slide show advance times are modified
;                   On Failure - @error = 1, Returns 0
;                              - @error = 2, Returns 2, Invalid type second
; Author(s):        Toady (Josh Bolton)
;
;===============================================================================
Func _PPT_SlideShowAdvanceTime(ByRef $obj, $intSecond)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	ElseIf IsInt($intSecond) = 0 Then
		SetError(2)
		Return 2
	Else
		$obj.Slides.Range.SlideShowTransition.AdvanceTime = $intSecond
	Endif
EndFunc
;===============================================================================
;
; Function Name:    _PPT_SlideShowAdvanceOnTime()
; Description:      Tells PowerPoint to use global advance times
; Parameter(s):     $obj - Presentation object
;                   $boolean - True or False
; Return Value(s):  On Success - Slides use advance times
;                   On Failure - @error = 1, Returns 0
; Author(s):        Toady (Josh Bolton)
;
;===============================================================================
Func _PPT_SlideShowAdvanceOnTime(ByRef $obj, $boolean)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.Slides.Range.SlideShowTransition.AdvanceOnTime = $boolean
	Endif
EndFunc
;===============================================================================
;
; Function Name:    _PPT_SlideShowLoopUntilStopped()
; Description:      Tells PowerPoint to loop presentation until manually stopped
; Parameter(s):     $obj - Presentation object
;                   $boolean - True or False
; Return Value(s):  On Success - Loop slide show until stopped
;                   On Failure - @error = 1, Returns 0
; Author(s):        Toady (Josh Bolton)
;
;===============================================================================
Func _PPT_SlideShowLoopUntilStopped(ByRef $obj, $boolean)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.SlideShowSettings.LoopUntilStopped = $boolean
	Endif
EndFunc
;===============================================================================
;
; Function Name:    _PPT_bAssistant()
; Description:      Tells PowerPoint to turn on or off the Assistant
; Parameter(s):     $obj - Presentation object
;                   $boolean - True or False
; Return Value(s):  On Success - Loop slide show until stopped
;                   On Failure - @error = 1, Returns 0
; Author(s):        Toady (Josh Bolton)
;
;===============================================================================
Func _PPT_bAssistant(ByRef $obj, $boolean)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.Assistant.On = $boolean
	Endif
EndFunc

Func _PPT_SlideShowAdvanceMode(ByRef $obj, $intMode)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	ElseIf $intMode > 3 Or $intMode < 1 Then
		SetError(2)
		Return 2
	Else
		$obj.SlideShowSettings.ShowScrollbar = $intMode
	Endif
EndFunc

Func _PPT_SlideShowDisplayScrollbars(ByRef $obj, $boolean)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.SlideShowSettings.ShowScrollbar = $boolean
	Endif
EndFunc

Func _PPT_SlideShowWithNarration(ByRef $obj, $boolean)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.SlideShowSettings.ShowWithNarration = $boolean
	Endif
EndFunc

Func _PPT_SlideShowWithAnimation(ByRef $obj, $boolean)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.SlideShowSettings.ShowWithAnimation = $boolean
	Endif
EndFunc

Func _PPT_SlideShowStartingSlide(ByRef $obj, $intSlide)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.SlideShowSettings.StartingSlide = $intSlide
	Endif
EndFunc

Func _PPT_SlideShowEndingSlide(ByRef $obj, $intSlide)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.SlideShowSettings.EndingSlide = $intSlide
	Endif
EndFunc

Func _PPT_PresentationSaveAs(ByRef $obj, $filename)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.SaveAs($filename)
	Endif
EndFunc

Func _PPT_PresentationClose(ByRef $obj)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.Close()
	Endif
EndFunc

Func _PPT_PresentationName($obj)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		Return $obj.Name()
	Endif
EndFunc

Func _PPT_PresentationSaved($obj)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		Return $obj.Saved
	Endif
EndFunc

Func _PPT_SlideAddPicture(ByRef $obj, $filepath, $left = 0, $top = 0, $width = 100, $height = 100)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	ElseIf FileExists($filepath) <> 1 Then
		SetError(2)
		Return 2 ;file does not exist
	ElseIf $left = "" Or $top = "" Or $width = "" Or $height = "" Then
		SetError(3)
		Return 3
	ElseIf IsInt($left+$top+$width+$height) <> 1 Then
		SetError(4)
		Return 4 ;All parameters have to be integer
	Else
		$obj.Shapes.AddPicture($filepath, 0, 1,150, 150, 500, 350)
	Endif
EndFunc

Func _PPT_SlideAddTable(ByRef $obj, $rows, $cols, $left = -1, $top = -1, $width = -1, $height = -1)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	ElseIf $left = "" Or $top = "" Or $width = "" Or $height = "" Then
		SetError(3)
		Return 3
	ElseIf IsInt($left+$top+$width+$height) <> 1 Then
		SetError(4)
		Return 4 ;All parameters have to be integer
	Else
		$obj.Shapes.AddTable($rows, $cols, $left, $top, $width, $height)
	Endif
EndFunc

Func _PPT_SlideAddTextBox(ByRef $obj, $left = 0, $top = 0, $width = 100, $height = 100)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.Shapes.AddTextBox(1, $left, $top, $width, $height)
	Endif
EndFunc

Func _PPT_SlideTextFrameGetText($obj, $intSlide, $intTextFrame)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		Local $text = $obj.Slides.Item($intSlide).Shapes.Range($intTextFrame).TextFrame.TextRange.Text
		Return $text
	Endif
EndFunc

Func _PPT_SlideShapeCount($obj, $intSlide)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		Return $obj.Slides.Item($intSlide).Shapes.Range.Count
	Endif
EndFunc

Func _PPT_SlideCreate($obj, $index, $layout)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		Local $SlideClass = $obj.Slides.Add($index, $layout)
		Return $SlideClass
	Endif
EndFunc

Func _PPT_PresentationOpen($obj, $filepath)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		Local $PresInterface = $obj.Presentations
		Local $objPres = $PresInterface.Open($filepath)
		Return $objPres
	Endif
EndFunc

Func _PPT_CreatePresentation($obj)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		Local $PropertyClass = $obj.Presentations
		Return $PropertyClass
	Endif
EndFunc

Func _PPT_PresentationAdd($obj)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		Local $objPres = $obj.Add(True) ; Presentation obj
		Return $objPres
	Endif
EndFunc

Func _PPT_PowerPointApp($visible = 1)
	Local $obj = ObjCreate("PowerPoint.Application")
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.Visible = $visible
		Return $obj
	Endif
EndFunc

Func _PPT_PowerPointQuit(ByRef $obj)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		$obj.Quit()
	Endif
EndFunc

Func _PPT_SlideCount($obj)
	If IsObj($obj) <> 1 Then
		SetError(1)
		Return 0
	Else
		Return $obj.Slides.Count
	Endif
EndFunc
