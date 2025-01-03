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
    @AppStorage("isAuthenticated") private var isAuthenticated: Bool = false
    @State private var user: ProtectedResponse
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
               _user = State(initialValue: ProtectedResponse(email: "preview@example.com", name: "Preview User"))
               _posts = State(initialValue: PostsResponse(yourPosts: [], restPosts: []))  // Simulated empty posts for preview
               isAuthenticated = true  // Simulate logged-in state for preview
           } else if let _ = getJWTFromKeychain(tokenType: "access_token") {
               _user = State(initialValue: ProtectedResponse(email: "user@example.com", name: "Authenticated User"))
               _posts = State(initialValue: PostsResponse(yourPosts: [], restPosts: []))  // Add real posts if necessary
               isAuthenticated = true
           } else {
               _user = State(initialValue: ProtectedResponse(email: "", name: ""))
               _posts = State(initialValue: PostsResponse(yourPosts: [], restPosts: []))  // Empty posts if not authenticated
               isAuthenticated = false
           }
       }
    
    var body: some View {
        Group {
            if isAuthenticated {
                MainView(user: user, posts: posts, logoutAction: {
                    // Delete both the access and refresh tokens
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
    let source: String // "rest" or "signalR"
}

class PostSelectionManager: ObservableObject {
    @Published var selectedPost: Post?
    @Published var isSheetPresented: Bool = false
}
struct YourPostsSection: View {
    let posts: [UnifiedPost]
    let columns: [GridItem]
//    @Binding var isSheetPresented: Bool
//    @State private var selectedPost: Post?
    @StateObject private var postSelectionManager = PostSelectionManager()
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            if !posts.isEmpty {
                Text(posts.first?.source == "yourPost" ? "Your Posts" : "Rest Posts")
                    .font(.headline)
                    .padding(.vertical, 8)

                LazyVGrid(columns: columns, spacing: 16) {
                    ForEach(posts) { post in
                        RideInfoButton(
                            peopleAssembled: "\(post.members.count)/\(post.maxPeople)",
                            destination: post.description,
                            minutesago: calculateMinutesAgo(from: post.createdAt),
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
                                SheetContent(post: selectedPost)
                            }
                        }
                    }
                }
                .padding()
            }
        }
    }

    private func calculateMinutesAgo(from createdAt: String) -> String {
        // Try ISO8601DateFormatter first
        let isoDateFormatter = ISO8601DateFormatter()
        isoDateFormatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        isoDateFormatter.timeZone = TimeZone(secondsFromGMT: 0)
        
        if let postDate = isoDateFormatter.date(from: createdAt) {
            let timeInterval = Date().timeIntervalSince(postDate)
            let minutesAgo = Int(timeInterval / 60)
            return "\(minutesAgo)"
        }
        
        // Fallback to DateFormatter for optional fractional seconds
        let fallbackFormatter = DateFormatter()
        
        // Try parsing with or without fractional seconds
        fallbackFormatter.dateFormat = "yyyy-MM-dd'T'HH:mm:ss" // Without fractional seconds
        fallbackFormatter.timeZone = TimeZone(secondsFromGMT: 0)
        
        if let postDate = fallbackFormatter.date(from: createdAt) {
            let timeInterval = Date().timeIntervalSince(postDate)
            let minutesAgo = Int(timeInterval / 60)
            return "\(minutesAgo)"
        }
        
        // If parsing fails, try with fractional seconds format
        fallbackFormatter.dateFormat = "yyyy-MM-dd'T'HH:mm:ss.SSS" // With fractional seconds
        
        if let postDate = fallbackFormatter.date(from: createdAt) {
            let timeInterval = Date().timeIntervalSince(postDate)
            let minutesAgo = Int(timeInterval / 60)
            return "\(minutesAgo)"
        }
        
        // Log failure and return 0
        print("Failed to parse date: \(createdAt)")
        return "0"
    }}
//struct PostGrid: View {
//    let posts: [UnifiedPost] // Combined posts
//    let columns: [GridItem]
//    @Binding var isSheetPresented: Bool
//
//    var body: some View {
//        LazyVGrid(columns: columns, spacing: 16) {
//            ForEach(posts, id: \.id) { post in
//                RideInfoButton(
//                    peopleAssembled: "\(post.members.count)/\(post.maxPeople)",
//                    destination: post.description,
//                    minutesago: calculateMinutesAgo(from: post.createdAt),
//                    rideName: "Price: \(post.currentPrice)₸",
//                    status: "Active",
//                    color: post.source == "rest" ? .yellow : .blue,
//                    company: post.members.first?.name ?? "Unknown"
//                ) {
//                    isSheetPresented = true
//                }
////                .sheet(isPresented: $isSheetPresented) {
////                    SheetContent(post: )
////                        .onDisappear {
////                            // Handle sheet dismissal
////                        }
////                }
//            }
//        }
//    }
//    private func calculateMinutesAgo(from createdAt: String) -> String {
//        // Try ISO8601DateFormatter first
//        let isoDateFormatter = ISO8601DateFormatter()
//        isoDateFormatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
//        isoDateFormatter.timeZone = TimeZone(secondsFromGMT: 0)
//        
//        if let postDate = isoDateFormatter.date(from: createdAt) {
//            let timeInterval = Date().timeIntervalSince(postDate)
//            let minutesAgo = Int(timeInterval / 60)
//            return "\(minutesAgo)"
//        }
//        
//        // Fallback to DateFormatter for optional fractional seconds
//        let fallbackFormatter = DateFormatter()
//        
//        // Try parsing with or without fractional seconds
//        fallbackFormatter.dateFormat = "yyyy-MM-dd'T'HH:mm:ss" // Without fractional seconds
//        fallbackFormatter.timeZone = TimeZone(secondsFromGMT: 0)
//        
//        if let postDate = fallbackFormatter.date(from: createdAt) {
//            let timeInterval = Date().timeIntervalSince(postDate)
//            let minutesAgo = Int(timeInterval / 60)
//            return "\(minutesAgo)"
//        }
//        
//        // If parsing fails, try with fractional seconds format
//        fallbackFormatter.dateFormat = "yyyy-MM-dd'T'HH:mm:ss.SSS" // With fractional seconds
//        
//        if let postDate = fallbackFormatter.date(from: createdAt) {
//            let timeInterval = Date().timeIntervalSince(postDate)
//            let minutesAgo = Int(timeInterval / 60)
//            return "\(minutesAgo)"
//        }
//        
//        // Log failure and return 0
//        print("Failed to parse date: \(createdAt)")
//        return "0"
//    }
//}


struct ProfileView: View {
    @Binding var name: String
    @Binding var email: String
    let logoutAction: () -> Void

    var body: some View {
        VStack {
            Text("Name: \(name)")
                .font(.title)
                .padding()
            Text("Email: \(email)")
                .font(.subheadline)
                .foregroundColor(.gray)
                .padding()
            
            Button(action: logoutAction) {
                Text("Log Out")
                    .font(.headline)
                    .foregroundColor(.red)
                    .padding()
            }
        }
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
    @State private var email: String = "" // State variable for email
    @State private var isSheet1Presented = false
    @StateObject private var signalRManager = SignalRManager() // StateObject for SignalR
    @State private var isSheet2Presented = false
    let columns = [GridItem(.adaptive(minimum: 150))]
    @State private var user: ProtectedResponse
    @State private var posts: PostsResponse = PostsResponse(yourPosts: [], restPosts: [])
    var logoutAction: () -> Void // Accept logout closure
    
    init(user: ProtectedResponse, posts: PostsResponse, logoutAction: @escaping () -> Void) {
        _user = State(initialValue: user)
        _posts = State(initialValue: posts)
        self.logoutAction = logoutAction
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
                                        
                                        signalRManager.startConnection()
                                        PostAPIManager.shared.readposts { response in
                                            guard let response = response else {
                                                return
                                            }
                                            self.posts = response
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
                        } + signalRManager.posts.map { signalRPost in
                            UnifiedPost(
                                id: signalRPost.postId ,
                                members: signalRPost.members,
                                maxPeople: signalRPost.maxPeople,
                                description: signalRPost.description,
                                createdAt: signalRPost.createdAt,
                                currentPrice: signalRPost.currentPrice,
                                source: "signalR"
                            )
                        }

                        if !unifiedPosts.isEmpty {
                            // Separate user posts and rest posts
                            YourPostsSection(
                                posts: unifiedPosts.filter { $0.source == "yourPost" },
                                columns: columns
                                //   isSheetPresented: $isSheet1Presented
                            )

                            YourPostsSection(
                                posts: unifiedPosts.filter { $0.source == "rest" },
                                columns: columns
                              //  isSheetPresented: $isSheet1Presented
                            )
                        }

                        Spacer()
                    }
                }
                .tabItem {
                    Label("Rides", systemImage: "car.front.waves.up.fill")
                }
                .onAppear {
                    signalRManager.startConnection()
                    PostAPIManager.shared.readposts { response in
                        guard let response = response else {
                            return
                        }
                        self.posts = response
                        print("Posts fetched successfully: \(response)") // Add this line here
                    }
                }

                // Profile Tab
                ProfileView(
                    name: $name,
                    email: $email,
                    logoutAction: logoutAction
                )
                .tabItem {
                    Label("Profile", systemImage: "person.crop.circle")
                }
                .onAppear {
                    name = user.name
                    email = user.email
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
                    

                    Text(" \(minutesago) min ago")
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
    let post: Post

    var body: some View {
        VStack {
            Text(post.description)
                .font(.title)
                .padding()

            Text("Price: \(post.currentPrice)₸")
                .font(.subheadline)
                .foregroundColor(.gray)
                .padding()

            // Add more details from the `Post` model as needed

            Spacer()
        }
        .padding()
    }
}


struct ContentView_Previews: PreviewProvider {
    static var previews: some View {
        ContentView(isPreview: true)  // Pass `true` to simulate being logged in
    }
}
