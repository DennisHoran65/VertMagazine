Magazine Subscriptions App

Project Layout
--An Objects namespace to create objects that match the magazine subscription API.
  Response Objects:
              CategoryListResponse
			  MagazineResponse     (contains array of Magazine)
			  SubscriberResponse   (contains array of Subscriber)
			  TokenResponse
Post Object:  Answer

	Flow of Program:
	      Sub Main calls RunProgram() for as many times as user decides to run.
		  
	      Step 1 - Get Token.  Save response token string for future calls
          Step 2 - Get List of subscribers.  Run it asynchronously so program does not 
          Step 3 - Get list of categories. Run synchronously.
          Step 4 - Get list of magazines.  Each category call will be asynchronous.
          Step 5 - wait for all tasks before building answer

		  Step 6 - Build the answer, by looping through subscribers and seeing how
		  many subscribers' category count matches the total number of categories.
          
		  Step 7 - Post the Answer, and write the Response to the screen