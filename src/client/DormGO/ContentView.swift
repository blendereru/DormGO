//
//  ContentView.swift
//  KBTU Go
//
//  Created by Райымбек Омаров on 16.11.2024.
//


import SwiftUI

struct User {
    var name: String
    var email: String
}

func getUserDetails() -> User {
    // In a real scenario, you would decode the JWT or call an API to fetch user details.
    return User(name: "Raiymbek Omarov", email: "raiymbek@example.com")
}

func deleteJWTFromKeychain(tokenType: String) -> Bool {
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrAccount as String: tokenType
    ]
    
    let status = SecItemDelete(query as CFDictionary)
    print("Keychain status after deleting token: \(status)") // Debugging log
    
    return status == errSecSuccess
}

struct ContentView: View {
    @StateObject private var signalRManager = SignalRManager()
    @AppStorage("isAuthenticated") private var isAuthenticated: Bool = false
    @State private var user: ProfileInfo
    @State private var posts: PostsResponse

//    init() {
//        // Check for JWT in Keychain
//        if let _ = getJWTFromKeychain() {
//            isAuthenticated = true
//        }
//    }
    init(isPreview: Bool = false) {
           // Initialize the `user` and `posts` with default values
           if isPreview {
               _user = State(initialValue: ProfileInfo(email: "preview@example.com", name: "Preview User", registeredAt: Date()))
               _posts = State(initialValue: PostsResponse(yourPosts: [], restPosts: []))  // Simulated empty posts for preview
               isAuthenticated = true  // Simulate logged-in state for preview
           } else if let _ = getJWTFromKeychain(tokenType: "access_token") {
               _user = State(initialValue: ProfileInfo(email: "user@example.com", name: "Authenticated User", registeredAt: Date()))
               _posts = State(initialValue: PostsResponse(yourPosts: [], restPosts: []))  // Add real posts if necessary
               isAuthenticated = true
           } else {
               _user = State(initialValue: ProfileInfo(email: "", name: "", registeredAt: Date()))
               _posts = State(initialValue: PostsResponse(yourPosts: [], restPosts: []))  // Empty posts if not authenticated
               isAuthenticated = false
           }
       }
    
    func retryLogoutAction() {
        // Assuming that you have the correct refresh_token and accessToken here
        let token = getJWTFromKeychain(tokenType: "access_token")!
        let refresh_token = getJWTFromKeychain(tokenType: "refresh_token")!
        
        PostAPIManager.shared.logoutfunc(refreshToken: refresh_token, accessToken: token)
        
        let accessTokenDeleted = deleteJWTFromKeychain(tokenType: "access_token")
        let refreshTokenDeleted = deleteJWTFromKeychain(tokenType: "refresh_token")
        
        if accessTokenDeleted && refreshTokenDeleted {
            print("Both access and refresh tokens have been deleted successfully.")
        } else {
            print("Failed to delete tokens.")
        }
        
        // Set isAuthenticated to false to log the user out
        isAuthenticated = false
    }
    var body: some View {
        Group {
            if isAuthenticated {
                MainView(user: user, posts: posts, signalRManager: signalRManager, logoutAction: {
                    // Delete both the access and refresh tokens
                    guard let token = getJWTFromKeychain(tokenType: "access_token") else {
                        print("Access token missing. Attempting to refresh token.")
                        PostAPIManager().refreshToken2 { success in
                            if success {
                                retryLogoutAction()
                            } else {
                                print("Unable to refresh token. Exiting.")
                            }
                        }
                        return
                    }
                    guard let refresh_token = getJWTFromKeychain(tokenType: "refresh_token") else {
                        print("Access token missing. Attempting to refresh token.")
                        PostAPIManager().refreshToken2 { success in
                            if success {
                                retryLogoutAction()
                            } else {
                                print("Unable to refresh token. Exiting.")
                            }
                        }
                        return
                    }


                    PostAPIManager.shared.logoutfunc(refreshToken: refresh_token, accessToken: token)
                    let accessTokenDeleted = deleteJWTFromKeychain(tokenType: "access_token")
                    let refreshTokenDeleted = deleteJWTFromKeychain(tokenType: "refresh_token")
                    
                    if accessTokenDeleted && refreshTokenDeleted {
                        print("Both access and refresh tokens have been deleted successfully.")
                    } else {
                        print("Failed to delete tokens.")
                    }
                    
                    // Set isAuthenticated to false to log the user out
                    isAuthenticated = false
                })
            } else {
                AuthenticationView(onAuthenticated: {
                    isAuthenticated = true
                })
            }
            
        }
        
    }
}

struct UnifiedPost: Identifiable {
    let id: String
    let members: [ProtectedResponse]
    let maxPeople: Int
    let description: String
    let createdAt: String
    let currentPrice: Double
    let source: String// "rest" or "signalR"
}

class PostSelectionManager: ObservableObject {
    @Published var selectedPost: Post?
    @Published var isSheetPresented: Bool = false
}
struct YourPostsSection: View {
    let posts: [UnifiedPost]
    let columns: [GridItem]
    @StateObject private var postSelectionManager = PostSelectionManager()
    let user: ProfileInfo
    var body: some View {
        print("Posts passed to YourPostsSection:", posts.map { $0.source })
        return VStack(alignment: .leading, spacing: 16) {
            if !posts.isEmpty {
                Text(posts.first?.source == "yourPost" ? "Your Posts" :
                     posts.first?.source == "joined" ? "Joined" : "Rest Posts")
                    .font(.headline)
                    .padding(.vertical, 8)

                LazyVGrid(columns: columns, spacing: 16) {
                    ForEach(posts) { post in
                        RideInfoButton(
                            peopleAssembled: "\(post.members.count)/\(post.maxPeople)",
                            destination: post.description,
                            minutesago: formatTime(from: post.createdAt),
                            rideName: "Price: \(post.currentPrice)₸",
                            status: "Active",
                            color: post.source == "rest" ? .yellow : .blue,
                            company: post.members.first?.name ?? "Unknown"
                        ) {
                            PostAPIManager.shared.readPost(postId: post.id) { postDetails in
                                DispatchQueue.main.async {
                                    if let postDetails = postDetails {
                                        postSelectionManager.selectedPost = postDetails
                                        postSelectionManager.isSheetPresented = true
                                    }
                                }
                            }
                        }
                        .sheet(isPresented: $postSelectionManager.isSheetPresented) {
                            if let selectedPost = postSelectionManager.selectedPost {
                                if post.source == "joined" {
                                    SheetContent_joined(post: selectedPost, user: user)
                                } else {
                                    SheetContent(post: selectedPost, isUserPost: post.source == "yourPost", user: user)
                                }
                            }
                        }
                    }
                }
                .padding()
            }
        }
    }

    private func formatTime(from createdAt: String) -> String {
        // ISO8601 formatter for standard formats
        let isoDateFormatter = ISO8601DateFormatter()
        isoDateFormatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds, .withTimeZone]
        isoDateFormatter.timeZone = TimeZone(secondsFromGMT: 0)

        if let postDate = isoDateFormatter.date(from: createdAt) {
            let timeFormatter = DateFormatter()
            timeFormatter.dateFormat = "HH:mm"
            timeFormatter.timeZone = TimeZone(secondsFromGMT: 0)
            return timeFormatter.string(from: postDate)
        }
        
        // Fallback formatter for cases where there is no timezone information
        let fallbackFormatter = DateFormatter()
        fallbackFormatter.dateFormat = "yyyy-MM-dd'T'HH:mm:ss" // This format expects no timezone info
        fallbackFormatter.timeZone = TimeZone(secondsFromGMT: 0)

        if let postDate = fallbackFormatter.date(from: createdAt) {
            let timeFormatter = DateFormatter()
            timeFormatter.dateFormat = "HH:mm"
            timeFormatter.timeZone = TimeZone(secondsFromGMT: 0)
            return timeFormatter.string(from: postDate)
        }
        
        // Additional fallback for specific timezone formats, if necessary
        fallbackFormatter.dateFormat = "yyyy-MM-dd'T'HH:mm:ssZZZZZ" // For formats with timezone offsets like +0600
        if let postDate = fallbackFormatter.date(from: createdAt) {
            let timeFormatter = DateFormatter()
            timeFormatter.dateFormat = "HH:mm"
            timeFormatter.timeZone = TimeZone(secondsFromGMT: 0)
            return timeFormatter.string(from: postDate)
        }
        
        print("Failed to parse date: \(createdAt)")
        return "Invalid time"
    }
}




struct ProfileView: View {
    @Binding var name: String
    @Binding var email: String
    @Binding var registeredAt: Date
    let logoutAction: () -> Void

    
    private var formattedDate: String {
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        formatter.timeStyle = .none
        return formatter.string(from: registeredAt)
    }
    var body: some View {
        VStack(spacing: 20) {
            // Title Text
            Text("Profile")
                .font(.largeTitle)
                .fontWeight(.bold)
                .padding(.top, 40)

            // Name and Email Information
            VStack(spacing: 10) {
                Text("Name: \(name)")
                    .font(.title2)
                    .fontWeight(.semibold)
                    .foregroundColor(.primary)
                    .padding(.horizontal)

                Text("Email: \(email)")
                    .font(.subheadline)
                    .foregroundColor(.secondary)
                    .padding(.horizontal)
                
                Text("Registered At: \(formattedDate)") // Display formatted date
                                   .font(.subheadline)
                                   .foregroundColor(.secondary)
                                   .padding(.horizontal)
            }

            Spacer()

            // Logout Button with more style
            Button(action: logoutAction) {
                Text("Log Out")
                    .font(.headline)
                    .foregroundColor(.white)
                    .padding()
                    .frame(maxWidth: .infinity)
                    .background(Color.red)
                    .cornerRadius(10)
                    .shadow(radius: 5)
            }
            .padding(.horizontal)

        }
        .padding()
      

        .onAppear {
            APIManager.shared.sendProtectedRequest { protectedResponse in
                self.name = protectedResponse?.name ?? ""
                self.email = protectedResponse?.email ?? ""
            }
       
        }
    }
}

struct MainView: View {
    @State private var name: String = "" // State variable for name
    @State private var email: String = ""
    @State private var registeredAt: Date = Date()// State variable for email
    @State private var isSheet1Presented = false
 //   @StateObject private var signalRManager = SignalRManager()// StateObject for SignalR
    @ObservedObject var signalRManager: SignalRManager
    @State private var isSheet2Presented = false
    let columns = [GridItem(.adaptive(minimum: 150))]
    @State private var user: ProfileInfo
    @State private var posts: PostsResponse = PostsResponse(yourPosts: [], restPosts: [])
    @State private var joinedposts:PostsResponse_other = PostsResponse_other(postsWhereMember : [])
    var logoutAction: () -> Void // Accept logout closure
    
    init(user: ProfileInfo, posts: PostsResponse, signalRManager: SignalRManager, logoutAction: @escaping () -> Void) {
        _user = State(initialValue: user)
        _posts = State(initialValue: posts)
        self.signalRManager = signalRManager
        self.logoutAction = logoutAction
     
    }
    private func setupSignalRCallbacks() {
        signalRManager.onPostCreated = { newPost in
            // Create mutable copy
            var newPosts = self.posts
            print("lmaboo")
            if newPost.creator.email == self.user.email {
                print("new")
                newPosts.yourPosts.append(newPost)
            } else {
                newPosts.restPosts.append(newPost)
            }
            
            // Assign back to trigger view update
            self.posts = newPosts
        }
        
        signalRManager.onPostUpdated = { updatedPost in
            var newPosts = self.posts
            
            if let index = newPosts.yourPosts.firstIndex(where: { $0.postId == updatedPost.postId }) {
                newPosts.yourPosts[index] = updatedPost
            } else if let index = newPosts.restPosts.firstIndex(where: { $0.postId == updatedPost.postId }) {
                newPosts.restPosts[index] = updatedPost
            }
            
            self.posts = newPosts
        }
        
        signalRManager.onPostDeleted = { postId in
            var newPosts = self.posts
            newPosts.yourPosts.removeAll { $0.postId == postId }
            newPosts.restPosts.removeAll { $0.postId == postId }
            self.posts = newPosts
        }
    }
    var body: some View {
        VStack {
            TabView {
                // Rides Tab
                ScrollView { // Make the Rides tab scrollable
                    VStack {
                        HStack {
                            Spacer()
                            Button(action: {
                                isSheet2Presented = true
                            }) {
                                Text("Publish")
                                    .frame(width: 120, height: 50)
                                    .background(Color.blue)
                                    .foregroundColor(.white)
                                    .cornerRadius(30)
                            }
                            .padding(.trailing, 16)
                            .sheet(isPresented: $isSheet2Presented) {
                                PublishContent()
                                    .onDisappear {


                                        PostAPIManager.shared.readposts { response in
                                            guard let response = response else {
                                                print("Failed to fetch posts")
                                                return
                                            }
                                            
                                            // Assign the fetched posts to your local variable
                                            self.posts = response
                                            
                                            // Start the SignalR connection only after posts are fetched
                                            signalRManager.startConnection()
                                        }
                                                                          }
                            }
                        }
                        .padding(.top, 16)

                        // Combine and sort posts, ensuring that 'your posts' come first
                        let unifiedPosts: [UnifiedPost] = posts.yourPosts.map { yourPost in
                            UnifiedPost(
                                id: yourPost.postId ,
                                members: yourPost.members,
                                maxPeople: yourPost.maxPeople,
                                description: yourPost.description,
                                createdAt: yourPost.createdAt,
                                currentPrice: Double(yourPost.currentPrice),
                                source: "yourPost"
                            )
                        } + posts.restPosts.map { restPost in
                            UnifiedPost(
                                id: restPost.postId ,
                                members: restPost.members,
                                maxPeople: restPost.maxPeople,
                                description: restPost.description,
                                createdAt: restPost.createdAt,
                                currentPrice: Double(restPost.currentPrice),
                                source: "rest"
                            )
                        }

                        if !unifiedPosts.isEmpty {
                            // Separate user posts and rest posts
                            YourPostsSection(
                                posts: unifiedPosts.filter { $0.source == "yourPost" },
                                columns: columns, user:user
                                //   isSheetPresented: $isSheet1Presented
                            )

                           
                            YourPostsSection(
                                                     posts: unifiedPosts.filter { $0.source == "rest"},
                                                     columns: columns, user: user
                                                 )
                        }

                        Spacer()
                    }
                }
                .tabItem {
                    Label("Rides", systemImage: "car.front.waves.up.fill")
                }
                .onAppear {
                    setupSignalRCallbacks()
                    
                    PostAPIManager.shared.readposts { response in
                           guard let response = response else { return }
                           DispatchQueue.main.async {
                               self.posts = response
                               self.signalRManager.startConnection()
                           }
                       

                       
                    

                                    }
                    
                }

                
                ScrollView{
                    VStack{
                        let unifiedPosts: [UnifiedPost] = joinedposts.postsWhereMember.map { yourPost in
                            UnifiedPost(
                                id: yourPost.postId ,
                                members: yourPost.members,
                                maxPeople: yourPost.maxPeople,
                                description: yourPost.description,
                                createdAt: yourPost.createdAt,
                                currentPrice: Double(yourPost.currentPrice),
                                source: "joined"
                            )
                            
                        }
                        if !unifiedPosts.isEmpty {
                            // Separate user posts and rest posts
                            YourPostsSection(
                                posts: unifiedPosts.filter { $0.source == "joined" },
                                columns: columns, user: user
                                //   isSheetPresented: $isSheet1Presented
                            )

                           
                         
                        }
                    }
                    }.tabItem {
                        Label("Joined", systemImage: "car.side.arrowtriangle.down.fill")
                    }.onAppear{
                        PostAPIManager.shared.read_other { response in
                            guard let response = response else {
                                return
                            }
                            self.joinedposts = response
                            print("Posts fetched successfully: \(response)") // Add this line here
                        }
                    }
                

                // Profile Tab
                ProfileView(
                    name: $name,
                    email: $email,
                    registeredAt: $registeredAt,
                    logoutAction: logoutAction
                )
                .tabItem {
                    Label("Profile", systemImage: "person.crop.circle")
                }
                .onAppear {
                    name = user.name
                    email = user.email
                    registeredAt = user.registeredAt
                }
            }
        }
    }
    
}
struct AuthenticationView: View {
    @State private var showLogin = true
    var onAuthenticated: () -> Void

    var body: some View {
        VStack {
            if showLogin {
                LoginView(onLoginSuccess: onAuthenticated)
            } else {
                RegistrationView(onRegistrationSuccess: onAuthenticated)
            }

            Button(action: {
                showLogin.toggle()
            }) {
                Text(showLogin ? "Switch to Registration" : "Switch to Login")
                    .font(.headline)
                    .padding()
            }
        }
    }
}
struct RideInfoButton: View {
    let peopleAssembled: String
    let destination: String
    let minutesago: String
    let rideName: String
    let status: String
    let color: Color
    let company: String
    
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            VStack {
                HStack {
                    Text(destination)
                        .font(.title)
                        .fontWeight(.bold)
                        .foregroundColor(.white)

                    // Add clock icon and "minutes ago" text
                    

                    Text(" \(minutesago) ")
                        .font(.subheadline)
                        .foregroundColor(.white)
                    
                    Image(systemName: "clock.arrow.circlepath")
                        .foregroundColor(.white)
                        .font(.subheadline) // Adjust the icon size to match the text
                }

              

               

                Text(rideName)
                    .font(.footnote)
                    .foregroundColor(.white)

                Text(peopleAssembled)
                    .foregroundColor(.white)
        
            }
            .frame(width: 180, height: 180) // Round button size
            .background(
                RoundedRectangle(cornerRadius: 30) // Large corner radius for round shape
                    .fill(color)
                    .opacity(0.7)
            )
            
        }
    }
}

struct SheetContent: View {
    @StateObject private var chatHub: ChatHub
   let user: ProfileInfo
    @State private var isMembersPopover: Bool = false
    let post: Post
    let isUserPost: Bool
    @State private var isPublishSheetPresented: Bool = false
    let shared = PostAPIManager()
    init(post: Post, isUserPost: Bool, user: ProfileInfo) {
        self._chatHub = StateObject(wrappedValue: ChatHub(postId: post.postId))
        self.post = post
        self.isUserPost = isUserPost
        self.user = user
    }
    var body: some View {
        VStack(spacing: 16) {
            postDetailsSection
            if !isUserPost {
                actionButtonsSection
            }
           
            
            if isUserPost {
                userPostButtonsSection
            }
            ChatView(chatHub: chatHub, user: user, postId: post.postId)
            Spacer()
        }
        .padding()
    }

    // MARK: - Post Details Section
    private var postDetailsSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(post.description)
                .font(.title)
                .padding(.bottom, 4)
            
            MapView2(latitude: post.latitude, longitude: post.longitude)
                            .frame(height: 300)
                            .cornerRadius(20) // Set the corner radius to make the map rounded
                            .shadow(radius: 5)
            
            Text("Price: \(post.currentPrice)₸")
                .font(.subheadline)
                .foregroundColor(.gray)
            
            Button(action: {
                        self.isMembersPopover = true
               }) {
                   Text("Members")
                       .padding()
               }
               .background(Color.blue) // Applies to the entire button
                       .foregroundColor(.white)
                       .cornerRadius(8)
                       .popover(isPresented: $isMembersPopover) {
                           VStack(alignment: .leading){
                               Text("Dividers")
                                   .font(.headline)
                                   .padding()
                               Divider()
                               
                               ScrollView{
                                   VStack(alignment:.leading,spacing:8){
                                       if post.members.isEmpty{
                                           Text("No members yet")
                                               .foregroundColor(.gray)
                                               .padding()
                                       } else{
                                           ForEach(post.members ){ member in
                                               Text(member.name)
                                               
                                           }
                                       }
                                   }
                               }
                           }}
        }
        .padding(.horizontal)
    }

    // MARK: - Action Buttons Section
    
    private var actionButtonsSection: some View {
        VStack(spacing: 8) {
            Button(action: {
                shared.join(postId: post.postId)
            }) {
                ActionButton(title: "Join", backgroundColor: .blue)
            }

         
        }
    }

    // MARK: - User Post Buttons Section
    private var userPostButtonsSection: some View {
        VStack(spacing: 8) {
            Button(action: {
                shared.deletePost(postId: post.postId)
            }) {
                ActionButton(title: "Delete", backgroundColor: .red)
            }

            Button(action: {
                isPublishSheetPresented.toggle()
            }) {
                ActionButton(title: "Update", backgroundColor: .green)
            }
            .sheet(isPresented: $isPublishSheetPresented) {
              
                UpdateContent(postId: post.postId, member: post.members) .onAppear {
                    // Print the members when the sheet appears
                    print("Members being passed to UpdateContent: \(post.members)")
                }
            }
        }
    }
}
struct SheetContent_joined: View {
    let post: Post
    let user: ProfileInfo
    @StateObject private var chatHub: ChatHub
    @State private var isMembersPopover: Bool = false
    @State private var isPublishSheetPresented: Bool = false
    let shared = PostAPIManager()
    init(post: Post, user: ProfileInfo) {
        self.post = post
        self.user = user
        _chatHub = StateObject(wrappedValue: ChatHub(postId: post.postId))
    }
    var body: some View {
        VStack(spacing: 16) {
            postDetailsSection
            actionButtonsSection

           
            ChatView(chatHub: chatHub, user: user, postId: post.postId)

            Spacer()
        }
        .padding()
    }

    private var postDetailsSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(post.description)
                .font(.title)
                .padding(.bottom, 4)
            MapView2(latitude: post.latitude, longitude: post.longitude)
                .frame(height: 300)
                .cornerRadius(20)
                .shadow(radius: 10)
            Text("Price: \(post.currentPrice)₸")
                .font(.subheadline)
                .foregroundColor(.gray)

            Button(action: {
                self.isMembersPopover = true
            }) {
                Text("Members")
                    .padding()
            }
            .background(Color.blue)
            .foregroundColor(.white)
            .cornerRadius(8)
            .popover(isPresented: $isMembersPopover) {
                VStack(alignment: .leading) {
                    Text("Dividers")
                        .font(.headline)
                        .padding()
                    Divider()
                    ScrollView {
                        VStack(alignment: .leading, spacing: 8) {
                            if post.members.isEmpty {
                                Text("No members yet")
                                    .foregroundColor(.gray)
                                    .padding()
                            } else {
                                ForEach(post.members) { member in
                                    Text(member.name)
                                }
                            }
                        }
                    }
                }
            }
        }
        .padding(.horizontal)
    }

    private var actionButtonsSection: some View {
        VStack(spacing: 8) {
            Button(action: {
                shared.unjoin(postId: post.postId)
            }) {
                ActionButton(title: "Unjoin", backgroundColor: .blue)
            }
        }
    }
}



struct ChatView: View {
    @State private var messages: [Message] = []
    @State private var newMessage: String = ""
 //   @StateObject private var chatHub = ChatHub()
    @ObservedObject var chatHub: ChatHub
    let user: ProfileInfo
    let postId: String
    let sentAt: String
    init(chatHub: ChatHub,user: ProfileInfo, postId: String) {
        self.chatHub = chatHub
         // 👈 Setup
    
        self.user = user
        self.postId = postId
        self.sentAt = ""
    
        
    }
    
    
    private func setupChatHub() {
        chatHub.onMessageReceived = { receivedPostId, message in
            if receivedPostId == postId {
                print("multo")
                messages.append(message)
            }
        }
    }
    
    private func fetchMessages() {
        PostAPIManager.shared.fetchMessagesForPost(postId: postId) { fetchedMessages in
            if let fetchedMessages = fetchedMessages {
                messages = fetchedMessages
            }
        }
    }
    
    private func sendMessage() {
        guard !newMessage.isEmpty else { return }

        PostAPIManager.shared.sendMessageToPost(postId: postId, message: newMessage) { chatResponse in
            if let chatResponse = chatResponse {
             
                let sender = Sender(email: user.email, name: user.name)
                
                // Create a new Message object
                let newMessage = Message(
                    messageId: UUID().uuidString,
                    content: chatResponse.message.content,
                    sender: sender,
                    sentAt:  sentAt, updatedAt: nil
              
                )
                
                // Append the new Message to the messages array
                messages.append(newMessage)
                
                // Clear the input after sending the message
                self.newMessage = ""
            }
        }
    }
    
    var body: some View {
        VStack {
            List(messages) { message in
                VStack(alignment: .leading) {
                    Text(message.sender.email)
                        .font(.caption)
                        .foregroundColor(.gray)
                    Text(message.content)
                        .font(.body)
                }
                .padding(8)
            }
            
            HStack {
                TextField("Enter message...", text: $newMessage)
                    .padding(8)
                    .textFieldStyle(RoundedBorderTextFieldStyle())
                
                Button(action: {
                    sendMessage()
                }) {
                    Text("Send")
                        .padding(8)
                }
                .disabled(newMessage.isEmpty)
            }
            .padding(.horizontal)
            
        }
        .onAppear {
            setupChatHub()
                fetchMessages()
                chatHub.startConnection()
     
        }
    }
}
// MARK: - ActionButton Component
struct ActionButton: View {
    let title: String
    let backgroundColor: Color

    var body: some View {
        Text(title)
            .font(.headline)
            .foregroundColor(.white)
            .padding()
            .frame(maxWidth: .infinity)
            .background(backgroundColor)
            .cornerRadius(8)
    }
}
struct ContentView_Previews: PreviewProvider {
    static var previews: some View {
        ContentView(isPreview: true)  // Pass `true` to simulate being logged in
    }
}
