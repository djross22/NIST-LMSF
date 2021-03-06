# NIST-LMSF

NIST-LMSF is a top-level automation control program used in the NIST Living Measurement Systems Foundry. 

NIST-LMSF has three components: 
1. `LMSF Scheduler.exe` is the main user interface. It runs the automation control scripts, and is the main controller of an automation experiment. `LMSF Scheduler.exe` should typically be installed and run from the "main" automation control computer.
2. `Hamilton Remote.exe` is a program that runs on a remote computer. It receives commands from `LMSF Scheduler.exe` on the main computer (via the `RemoteHam()` command) and uses them to control a Hamilton liquid handler via the Hamilton Run Control program (`HxRun.exe`). Note that a Hamilton liquid handler can also be directly controlled from the main computer using the `Hamilton()` command (see below).
3. `LMSF_Gen5.exe` is a program that runs on a remote computer or on the main computer. It receives commands from `LMSF Scheduler.exe` on the main computer (via the `Gen5()` command) and uses them to control a Biotek plate reader via the Biotek Gen5 automation API.


## Getting Started
Build the solution in Visual Studio and use the installer (`\NIST LMSF\LMSF Controls\Release\LMSF Controls.msi`) to install the software components on the main and remote computers. Or, email me (david.ross@nist.gov), and I'll send you the latest installer.

The examples folder has an example script (`Sample user interaction script.lmsf`) that can be run without any instruments (Hamilton, Gen5, etc.).


Much of the following material was written in plain text, and has not yet been formatted nicely into .md format. Sorry.

## Writing scripts
Scripts can be written in any text editor. The `LMSF Scheduler.exe` has a very basic text editor on the left side of the window (the "Steps Input:" filed).
Script files can be saved and opened using the normal Windows-style menus at the top of the application. For most editing, though, I reccomend an alternate text editor, like Notepad++.

Scripts are written with one command (i.e., automation step) per line. 
Blank lines in the script are ignored.
Lines can also be commented out with two forward slashes at the start of the line ("//"). Commented lines will be ignored.
A line starting with "#" will also be ignored unless it is in a script file that is read and run via the ReadScript() command.
	The "#InsertVariables" line is used to indicated where to insert variable setting commands within a script file that will be run via the ReadScript() command.

Before running a script, you can check to see if it is correctly written by clicking on the validate button (top menu bar of the `LMSF Scheduler.exe` window, with the check-mark symbol).

Basic control of running scripts is done with a familiar set of buttons for "Play", "Pause", "Single Step", and "Abort".

Step syntax:
<Command>(<parameter(s)>)

The parameters for a Command are enclosed in parentheses "(" and ")". For Commands that take multiple parameters, the parameters are separated by commas.
    White space before and after the commas is ignored.

The parsing of Commands and parameters is generally case sensitive.

Parameters can generally contain any character except "," which is reserved for use as a parameter separator. The one exception to this is the parameters of the Set() Command, which can contain commas.

Parameters can contain key references, demarked by "{" and "}". The parser will replace the text, "{<key>}" with "<value>", where <key> and <value> are a key-value pair in the Metadata Dictionary (see below).
	For example, the input step:
		UserPrompt(Test, abc {testKey} def {testKey} ghi)
	gets parsed as:
		UserPrompt(Test, abc testValue def testValue ghi)
Key references that are not in the Metadata Dictionary will result in a Validation failure.


There is a drop-down combo-box control at the top left of the LMSF Scheduler GUI window that contains all of the commands for quick access.


Commands:

ReadScript()
	Reads another .lmsf script file and runs it as if it were typed into the Steps Input box.
	ReadScript cannot be used inside an If() statement (see below)
	The <file path> must be an existing path to a .lmsf script file.
		The <file path> parameter cannot contain any key references.
	Variables are set using a normal "a = b" syntax.
		To reference an item in the Metadata Dictionaries, use curly brackets: "a = {key}"

	Syntax:
		ReadScript(<file path>, {<variables to be set (optional)>})

	Examples:
		ReadScript(C:\Program Files (x86)\HAMILTON\Methods\GSF-IMS Project\Double 2D gradient plus 1000x bacteria.lmsf)
		ReadScript(C:\Users\djross\Documents\temp\Math test.lmsf, op = {b})


Overlord()
	Runs an Overlord procedure

	Syntax:
		Overlord(<file path>, {<variables to be set (optional)>})

	Examples:
		Overlord(C:\Program Files (x86)\PAA\Overlord3\Procedures\Common\Add Lid.ovp)
		Overlord(C:\Program Files (x86)\PAA\Overlord3\Procedures\Common\Add Lid.ovp, [Plates] 10 [Barcode] "12345678")


Hamilton()
	Runs a Hamilton method (on the local computer with Hamilton Venus installed).
	Also saves the Metadata Dictionary (metaDictionary) to a parameters file that can be read by a Hamilton Venus program (C:\Program Files (x86)\HAMILTON\LMSF_FrontEnd\parameters.csv).
	The method file parameter shuold point to the .hsl file associated with the Hamitlon Venus method.

	Syntax:
		Hamilton(<method file path>)

	Examples:
		Hamilton(C:\Program Files (x86)\HAMILTON\Methods\GSF-IMS Project\Cell gradient plate.lmsf.hsl)


RemoteHam()
	Runs a remote Hamilton method (on a different computer with Hamilton Venus installed).
	Also saves the Metadata Dictionary (metaDictionary) to a parameters file that can be read by the Hamilton Venus program on the remote computer (\\<remote IP address>\LMSF_FrontEnd\parameters.csv).
	The first argument is the name of the Hamilton.
		The valid Hamilton names are "S-Cell-STAR".
	The second argument is the command to send to the Hamilton.
		The valid commands that can be sent are "RunMethod" and "ReadCounters".
	If the second argument is RunMethod, the Remote Hamilton program on the remote computer will run the specified method.
		For the RunMethod command, one additional argument is required:
			The path to the method file to be run.
				The path is read from the point of view of the computer controlling the Hamilton.
				So, the method path will usually start with the Methods folder on that computer: "C:\Program Files (x86)\HAMILTON\Methods\..."
		Note: after calling a "RemoteHam(<Hamilton name>, RunMethod, ...)", the appropriate WaitFor() command to use is WaitFor(<Hamilton name>)

	If the second argument is ReadCounters, the Remote Hamilton program on the remote computer will run the Check Tip Counters method, and the LMSF Scheduler will read the status of the tip counters into the Metadata Dictionary.
		The Metadata Dictionary keys that will be written to are:
			tips1000Status1 = status of rack 1 (left side) for 1000 uL tips (1 = has tips; 0 = empty)
			tips1000Status2 = status of rack 2 (right side) for 1000 uL tips (1 = has tips; 0 = empty)
			tips1000Total = total number of 1000 uL tips in both racks
			tips300Status1 = status of rack 1 (left side) for 300 uL tips (1 = has tips; 0 = empty)
			tips300Status2 = status of rack 2 (right side) for 300 uL tips (1 = has tips; 0 = empty)
			tips300Total = total number of 300 uL tips in both racks
			tips50Status1 = status of rack 1 (left side) for 50 uL tips (1 = has tips; 0 = empty)
			tips50Status2 = status of rack 2 (right side) for 50 uL tips (1 = has tips; 0 = empty)
			tips50Total = total number of 50 uL tips in both racks
			tipsOffsetStatus1 = status of rack 1 (only rack) for 1000 uL Offset Pickup tips (1 = has tips; 0 = empty)
			tipsOffsetStatus2 = status of rack 2 for 1000 uL Offset Pickup tips; with the current deck configuration, there is no rack 2, so this should always be zero.
			tipsOffsetTotal = total number of 1000 uL tips in the Offset Pickup rack
		The status and tip total Dictionary entries can be used to make decisions on tip loading or User Prompts.
			For example:
				RemoteHam(S-Cell-STAR, ReadCounters)
				If({tips1000Status1} == 0, UserPrompt(Add More Tips, Add additional 1000 uL tips.))

	If the second argument is RunMethod, and the method ends with "Edit Tip Counters.hsl", the LMSF scheduler will run the method and then read the status of the tip counters into the Metadata Dictionary. 

	Syntax:
		RemoteHam(<Hamilton name>, <Hamilton command>, <file path>)

	Examples:
		RemoteHam(S-Cell-STAR, RunMethod, C:\Program Files (x86)\HAMILTON\Methods\GSF-IMS Project\Cell gradient plate.lmsf.hsl)
		RemoteHam(S-Cell-STAR, ReadCounters)


Gen5()
	Controls an instance of the LMSF_Gen5 program (with communication via TCP - so either for local reader or reader on another computer with network connection).
	The first argument is the name of the reader.
		The valid reader names are "Neo", "Epoch1", "Epoch2", "Epoch3", and "Epoch4".
	The second argument is the command to send to the reader.
		The valid commands that can be sent are "CarrierIn", "CarrierOut", and "RunExp".
	If the second argument is CarrierOut or CarrierIn, the reader does what you would expect in response.
		Note that if the carrier is out when a RunExp command is sent, the carrier will automatically close before starting the run.
	If the second argument is RunExp, the LMSF_Gen5 program will create and run a new experiment.
		For the RunExp command, three additional arguments are required:
			The path to the protocol file to be run.
				The path is read from the point of view of the computer controlling the reader.
				So, to use a protocol from the master Protocols directroy on the main computer, the protocol path will have to start with the IP adddress: "\\127.6.167.34\..."
			The experiment ID. This is used to automatically generate file names for the Gen5 experiment file (.xpt) and the data export file (.txt).
				The LMSF_Gen5 program automatically appends the reader name to the experiment ID to create the .xpt and export file names.
					Note that if more than one read needs to be run with the same experiment ID, then use "{experimentId}-read 1", "{experimentId}-read 2", etc. for the <experiment ID> argument.
			The path to the folder where the Gen5 experiment and exported data will be saved.
				The save folder path is also read from the point of view of the computer controlling the reader.

	Syntax:
		Gen5(<reader name>, <gen5 command>, {<protocol path>, <experiment ID>, <save folder path>})

	Examples:
		Gen5(Epoch1, CarrierOut)
		Gen5(Epoch2, CarrierIn)
		Gen5(Epoch1, RunExp, C:\Users\Public\Documents\Protocols\Growth Plate w Membrane 4h.37C.1C.prt, {experimentId}, C:\Shared Files\Data\GSF-IMS-coli\2019-02-04-0740_IPTG_gradient)


Timer()
	Starts a timer that runs for a specified amount of time, or until a specified time or date-time.
	Parsing of time or date-time strings is done using: https://docs.microsoft.com/en-us/dotnet/api/system.datetime.tryparse?view=netframework-4.7.2.
	With a time or date-time parameter, the Parser also checks to be sure the time or date-time is in the future.

	Syntax:
		Timer(<time in seconds (integer)>)
		Timer(<parsable time or date-time string>)

	Examples:
		Timer(20)
		Timer(5)
		Timer(2000)
		Timer(7:30pm)
		Timer(2019-01-25 7:30pm)


WaitFor()
	Pauses execution and waits for a previous step to finish
	The <process to wait for> argument can be either "Overlord", "Hamilton", "Timer", or the name of a remote instrument that is connected (see the Gen5() and RemoteHam() command descriptions).
	If the <process to wait for>  is a remote instrument, there are two additional optional parameters, <write end time> and <ping interval>.
		The <write end time> is a boolean used to set whether or not the finish time of the remote process is appended to the XML node for that process.
			Some remote processes don't have XML nodes, (e.g. CarrierIn), so it is not appropiate to try to append a finish time.
			If <write end time> is any string other than "false" or "False", or if there is no second paramter, the finish time will be appended.
		The <ping interval> parameter is an integer that sets the time (in milli-seconds) between "pings" to the remote process to check it's status.
			If no <ping interval> is set in the WaitFor command, the default value (1000) will be used.

	Syntax:
		WaitFor(<process to wait for>, {<write end time>, <ping interval>})

	Examples:
		WaitFor(Overlord)
		WaitFor(Hamilton)
		WaitFor(Epoch1)
		WaitFor(S-Cell-STAR)
		WaitFor(Timer)
		WaitFor(Epoch1, false)
		WaitFor(S-Cell-STAR, true, 3000)


NewXML()
	Starts a new XML metadata document, and opens a user dialog box to get the project identifier.
	The project identifier is saved in both the XML document and in the Metadata Dictionary (with the key "projectId").
	Also, adds the protocol starting date and time to the Metadata Dictionary (key = "startDateTime") in a form that is suitable for use as an identifier: "yyyy-MM-dd-HHmm".
	Also, adds the protocol starting date to the Metadata Dictionary (key = "startDate") in a form that is suitable for use in file names/identifiers: "yyyy-MM-dd".
	Also, adds the XML file path to the Metadata Dictionary (key = "metaDataFilePath").

	The <protocol type> is meant to be a short, human-readable description of the function of the current protocol.
		It is also used to distinguish protocols in a multi-protocol experiment.
		The <protocol type> is saved in the Metadata Dictionary with the key "protocol type".

	Syntax:
		NewXML(<protocol type>)

	Examples:
		NewXML(growth plate prep)
		NewXML(cytometry plate prep)


AppendXML()
	Similar to NewXML, but opens an existing XML metadata document and adds a new protocol to it. By default, AppendXML() opens a user dialog to choose the XML file to append to.
	Also, adds the protocol starting date and time to the Metadata Dictionary (key = "startDateTime") in a form that is suitable for use as an identifier: "yyyy-MM-dd-HHmm".
	Also, adds the protocol starting date to the Metadata Dictionary (key = "startDate") in a form that is suitable for use in file names/identifiers: "yyyy-MM-dd".
	Also, adds the XML file path to the Metadata Dictionary (key = "metaDataFilePath").
	If the second (optional) parameter is "NoUser", then the default XML metadata document is used without getting input from the user.
		The default XML metadata document can be set before calling AppendXML() by setting the "dataDirectory" entry in the Metadata Dictionary.
			For example: Set(dataDirectory, {newDefaultDirectory})

	Syntax:
		AppendXML(<protocol type>, {NoUser (optional)})

	Examples:
		AppendXML(growth plate prep)
		AppendXML(cytometry plate prep)


SaveXML()
	Saves the XML metadata document - in the directory set by the NewXML(), AppendXML(), or GetExpId() command.
	By default, this command also appends the "protocol finished" node to the dateTime node in the XML document.
	If the optional argument, "not finished" is used, the "protocol finished" node will not be appended.
	This method also writes a .lmsf script file to the same directory with the list of steps that have been run for the protocol. 
		This output script file also includes steps that were run manually during a pause.

	Syntax:
		SaveXML({not finished (optional)})

	Examples:
		SaveXML()
		SaveXML(not finished)


LoadXML()
	Loads an existing XML metadata document into memory, but does nothing else. This is useful for restartting an experiment that had to be stopped in the middle.

	Syntax:
		LoadXML(<xml file path>)

	Examples:
		LoadXML(C:\Shared Files\Data\LMSF-testing\2019-03-16-0804_cam_cam_iptg_2DGradients\2019-03-16-0804_cam_cam_iptg_2DGradients.xml)


AddXML()
	Adds a new node to the XML metadata document. 
	The name of the new node is <newNode>. It is added as a child to an existing node, <parentNode>.
	If the <parentNode> does not exist, it is added to the protocol node, with <newNode> as a child node.
	The inner text of the new node is set to <innerText>.

	Syntax:
		AddXML(<parentNode>, <newNode>, {<innerText (optional)>})

	Examples:
		AddXML(additive, newNode)
		AddXML(newNode, existingParentNode, new node inner text)


UserPrompt()
	Opens a message dialog box to prompt the user.
	The <message> string parameter is interpretted using string escape sequences ("\t" for tab, "\n" for new line).
		So, multi-line messages can be formateed and displayed.

	Syntax:
		UserPrompt(<title>, <message>, {<image file path (optional)>, <image width (optional)>})

	Examples:
		UserPrompt(Add Bacteria, Add bacteria to growth plate. Put plate in reader. Then click 'OK')
		UserPrompt(Add Bacteria, Add bacteria to growth plate and click 'OK', C:\Users\djross\Documents\temp\spinner.PNG)
		UserPrompt(Add Bacteria, Add bacteria to growth plate. \nPut plate in reader. Then click 'OK')
		UserPrompt(test, test, C:\Users\djross\Documents\temp\Overnight Double Gradient Layout.PNG, 1000)

		Set(msg1, Review the following, then click 'OK' to accept or 'Abort' to abort and start over:\n\n)
		Set(msg2, \tMedia: \t\t\t{media}\n\n \tBacteria: \t\t\t{strain1}, with {plasmid1}\n\n)
		Set(msg3, \tInducer: \t\t\t{inducer}, stock concentration: {inducerStock}\n \tLeft-side additive:  \t{leftAdditive}, stock concentration: {leftAdditiveStock}\n)
		Set(msg4, \tRight-side additive:  \t{rightAdditive}, stock concentration: {rightAdditiveStock}\n  \n)
		Set(msg5, \tExperiment ID: \n\t\t{experimentId}\n \tData directory: \n\t\t{dataDirectory}\n\n \tNotes:\n\t\t{note})
		UserPrompt(Review Protocol Details: {experimentId}, {msg1} {msg2} {msg3} {msg4} {msg5})


GetExpId()
	Opens a user dialog box to get the experiment identifier and data directory from the user.
	The dialog starts with default/suggested values defined by the parameters.
	If no <default data directory> parameter is given, "C:\Shared Files\Data\{projectId}" is used.
	By default, the command creates a new directory with the same name as the experiment ID,
		So, the default file will be saved as "<default data directory>\<default experiment ID>\<default experiment ID>.xml"
	The user can manually override the defaults in the dialog or accept them.
	After the user confirms, GetExpId also adds the experiment ID (key = "experimentId"), and the data directory (key = "dataDirectory") to the Metadata Dictionary
		GetExpId() also sets the file name and directory of the XML document for the experiment to <experimentId>.xml and <dataDirectory>, respectively.
			It then also, adds the XML file path to the Metadata Dictionary (key = "metaDataFilePath").

	Syntax:
		GetExpId(<default experiment ID>, {<default data directory (optional)>})

	Examples:
		GetExpId({startDateTime}_{strain1})
		GetExpID({startDateTime}_{inducer}_{leftAdditive}_{rightAdditive}_2DGradients)


GetTimeNow()
	Writes the current date-time into the metaDictionary using the format: "yyyy/MM/dd HH:mm:ss".

	Syntax:
		GetTimeNow(<date-time key>)

	Examples:
		GetTimeNow(readStartTime)


GetUserYesNo()
	Opens a user dialog box to get a Yes/No response from the user.
	The user's response is saved in the Metadata Dictionary with the key given as the first argument.
		The strings saved to the Dictionary are capitalized, "Yes" or "No".

	Syntax:
		GetUserYesNo(<key>, <title>, <prompt>)

	Examples:
		GetUserYesNo(test, title, choose yes or no)
		GetUserYesNo(gen5, Normalize With OD?, Use Gen5 output file to normalize cell density in cytometry plate?)


GetFile()
	Opens a user dialog box to get the location of a file from the user.
	The dialog starts by looking in the default data directory.
	If no <default data directory> parameter is given, "C:\Shared Files\Data" is used.
	The <file key> parameter is the key used to save the file path in the Metadata Dictionary.
	The <file prompt> is the prompt displayed to the user in the dialog box.
	The optional <file filter> parameter is a file filter string used to select only specific types of files (by their extensions).
		Some examples of valid file filter strings are:
			XML documents (.xml)|*.xml"
			Office Files|*.doc;*.xls;*.ppt"
			Word Documents|*.doc|Excel Worksheets|*.xls|PowerPoint Presentations|*.ppt"
		For more details on file filters, see: https://docs.microsoft.com/en-us/dotnet/api/microsoft.win32.filedialog.filter?view=netframework-4.7.2

	Syntax:
		GetFile(<file key>, <file prompt>, {<file filter (optional)>, <default data directory (optional)>})

	Examples:
		GetFile(file, Select file for input data
		GetFile(file, Select file for input data, CSV files (.csv)|*.csv)
		GetFile(file, Select file for input data, CSV files (.csv)|*.csv, {dataDirectory})


Get()
	Opens a user dialog box to get metadata information from the user, and saves the result in the Metadata Dictionary.

	The list of valid metadata types is: "user", "media", "strain", "plasmid", "additive", "antibiotic", "project", "concentration", "note", "number", and "integer"

	The <key> parameter is meant to be a short, human-readable indentifier of the function of the media, strain, plasmid, etc. in the current experiment.
	It is also used as the key for storage of Get() result in the metaDictionary.

	For metadata type "concentration", the user dialog gets the numeric value and units for the concentration.

	In the XML output, the result of a "Get(concentration,...)" command is attached to the last additive or antibiotic XML output (if there is one).
	So, a "Get(concentration,...)" command should be immediately after the matching "Get(additive,..." command, so that the automated XML document script will attach them together correctly. 
	With a "Get(concentration, <key>)" command, the concentration is saved in the Metadata Dictionaries (both of them); 
		the Concentration is saved as an object with both value and units in the concDictionary; 
		the numeric value and units are also saved in the metaDictionary using the keys "<key>Conc" and "<key>Units".

	If no <message prompt> parameter is given, the Get command will use the default prompt, "Select the <key> for the experiment: "
	The Get command will also use the default prompt if the <message prompt> parameter is "default".

	The optional <note> parameter and use of the "note" metadata type are different:
		Using "note" as a metadata type gets notes from the user and adds them in a "note" node to the XML protocol node.
		The optional <note> parameter can be used by the protocol programmer to add notes to any of the other metadata type nodes.
		So, a "note" is created by the user when the automation protocol is run, whereas a <note> is created by the programmer when the protocol is written.

		Also, adding a <note> parameter to a "note" metadata doesn't do anything.



	Syntax:
		Get(<metadata type>, <key>, {<message prompt(optinal)>, <note (optional)>})

	Examples:
		Get(strain, strain1)
		Get(media, baseMedia)
		Get(additive, inducer)
		Get(additive, inducer, Select the inducer used for the left side of the plate:)
		Get(additive, inducer, default, note to add to the XML file)
		Get(concentration, inducerStock)
		Get(integer, stackTotal, Enter the number of plates in stack 7.)


Set()
	Directly sets an entry in the Metadata Dictionary.
	Unlike other commands, the <value> parameter of a Set() command can have commas in it.

	Syntax:
		Set(<key>, <value>)

	Examples:
		Set(count, 20)
		Set(strain, MG1655)
		Set(msg1, Add bacteria to growth plate, put plate in reader, and click 'OK'.\n\n)


Math()
	Performs basic math operations and sets an entry in the Metadata Dictionary with the result.
	The Math() command is meant for simple things like counting plates or keeping track of time intervals. 
	With numbers as inputs, it can be used for the basic binary operations, '+', '-', '*', '/', and '%'.
	Math() can also be used with date-time strings and/or time spans expressed in seconds.
		For this, the first argument in the math expression must be a parsable date-time string,
			and the second argument must be either a parsable date-time string or a number (in seconds).
		Only addition and subtraction are allowed with date-time arguments.
		The result of subtration of two parsable date-time strings is a time span in seconds
		The result of addition of a date-time string and a number is a new date time string with the number of seconds added (format: "yyyy/MM/dd HH:mm:ss")
	Math() can only perform a single binary operator per step.

	Syntax:
		Set(<key>, <expression>)

	Examples:
		Math(count, {count} + 1)
		Math(plateNumber, 2 * 3)
		Math(gradNumber, {count} % 3)
		Math(gradNumber, {gradNumber} + 1)
		Math(endTime, {startTime} + 3600)
		Math(timeInterval, {endTime} - {startTime})
		Math(time42, 10/06/2019 - 10/06/1969)


StartPrompt()
	Opens a message dialog box for the start of a protocol. Lists the protocol requirements and offers 'OK' or 'Abort'.
	The protocol title appears near the top of the dialog
	The list file is a text file with a list of requirements. The contents of the list file will be displayed in the dialog exactly as they are in the text file.

	Syntax:
		StartPrompt(<protocol title>, <list file path>)

	Examples:
		StartPrompt(Inducer Gradient Plus Bacteria, C:\Program Files (x86)\HAMILTON\Methods\GSF-IMS Project\Inducer gradient plus 1000x bacteria-list.txt)


If()
	Any other command can be preceded by an If() command, which can control whether or not the command is run.
	This can be used to change the protocol steps in response to user input.

	Syntax:
		If(<logical test>, <other valid command, with normal arguments>)

	Examples:
		If({a} == b, UserPrompt(Add Bacteria, {msg1}))
		If({num}==2, Get(strain, strain1))
		If({num}!=2, GetFile(file, Select file for input data, CSV files (.csv)|*.csv)
		If({isEnoughOffsetTips} == Yes, Hamilton(C:\Program Files (x86)\HAMILTON\Methods\Common\Tip Handling\Edit Tip Counter Offset_1000.hsl) )


CopyRemoteFiles()
	Attempts to copy data files from remote computers to local computer.
	If the dataDirectory is a sub-folder of C:\Shared Files\, CopyRemoteFiles() will look for any files in the dataDirectory on the remote computers (the ones that are connected) and attempt to copy them to the dataDirectory of the local computer

	Syntax:
		CopyRemoteFiles()

	Examples:
		CopyRemoteFiles()


ImportDictionary()
	Reads data from a text file (extension .txt) into the Metadata Dictionary.
	Each non-empty line of the text file should be a key-value pair separated by a comma
		Any lines that do not conform to the "<key>,<value>" format will be ignored.

	Syntax:
		ImportDictionary(<file path>)

	Examples:
		ImportDictionary(C:\Users\djross\Documents\temp\Test Dictionary.txt)


ExportDictionary()
	Writes data from  the Metadata Dictionary to a text file (extension .txt).
	For each entry in the dictionay the method writes a line in the text file with the format, "<key>,<value>".

	Syntax:
		ExportDictionary(<file path>)

	Examples:
		ExportDictionary(C:\Users\djross\Documents\temp\Test Dictionary.txt)
			

Metadata Dictionary:
	The LMSF Scheduler has two Dictionary objects that effectively give the user the ability to create, store, and access variables for use across multiple steps.
	The first dictionary is for storage of string identifiers, the second is for storage of concentrations.
	The Metadata Dictionaries (metaDictionary and concDictionary in the C# code) consist of key-value pairs. 
	A value can be saved in one of the dictionaries, either directly with a Set() command or using a user dialog command such as Get().
	Values in either dictionary can then be accessed using the "{key}" syntax in a subsequent parameter entry.
		The command parser first looks for a key in the string identifier dictionary (metaDictionary),
		and then looks in the concentration dictionary if the key is not found in the string identifier dictionary.
		So, it is possible to use the same key to store values in both dictionaries, but the parser will only be able to find the corresponding value in the string dictionary.
		It is recommended to use related, but not identical keys for matching additive identifiers and concentrations,
			for example, "Get(additive, inducer)" followed by "Get(concentration, inducerStock)"
			
		If the command parser finds the requested key in one of the dictionaries, it replaces the sub-string, "{<key>}", with the sub-string "<value>" in the parameter string.
		
		
## Remote connections
The LMSF Scheduler uses TCP client-server communication protocols to send commands to the LMSF_Gen5 program, both when it is running on the same computer and also when it's running on a different computer in the system. For the TCP communication to work, the client-server link needs to first be connected.
To set up a connection, run both the LMSF Scheduler program and the LMSF_Gen5 program (again, either on the same computer or on a separate one).
	
In the LMSF_Gen5 program, click on the button labeled "Switch to Remote". Then in the LMSF Scheduler program, select the instance of LMSF_Gen5 from the drop-down combo-box at the bottom right of the GUI window. Select the 'Yes' option in the pop-up. The message "client connected" should appear in the message text box of the LMSF_Gen5 program.
		
The LMSF Scheduler can also be used to control a remote connected STAR instrument. This is similar to the use of the LMSF_Gen5 program.
	To set up a connection to a remote STAR controller, run both the LMSF Scheduler program (on the main computer) and the Hamilton Remote program (on the STAR computer).
		In the Hamilton Remote program, click on the button labeled "Switch to Remote". Then in the LMSF Scheduler program, select the instance of the remote STAR computer from the drop-down combo-box at the bottom right of the GUI window. Select the 'Yes' option in the pop-up. The message "client connected" should appear in the message text box of the Remote Hamilton program.
		
	If the necessary remote connections are not established, a script that uses them will fail the validation check and so will not run.
	
	
