   M S B A M L   `   `   `  ����  MultiToolLoader.    (MultiToolLoader.Controls.TypingIndicator   5      W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5      R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      $��20��5      $��40��6   .��5      � .+�5   
   ��   �� Orientation$  
Horizontal=�6   $��Center=�6.   .+�5      A� -   5         A�Name$
 Dot1��5      $��6��6$   $��6��6.   
�������$��2q�6F   A� 5      -   5        $
 Dot2��5      $��6��6$   $��6��6.   
�������$��2q�6F   A� 5	      -   5        $
 Dot3��5	      $��6��6$   $��6��6.   
�������$��2q�6F   5
      5       � Triggers 5      :� 5   
    :� RoutedEvent$ UserControl.Loaded��6   .[�5      �� .��5      ��  g� RepeatBehavior$ Forever��6   .��5      W�  �� 
TargetName$
 Dot1��5       �� TargetProperty$ Opacity�5       W� From 1.05      	 W� To 0.36$   	 g� Duration$	 0:0:0.6D�6-   
 g� AutoReverse$

 True��5       g� 	BeginTime	 0:0:05      W� 5      $
 Dot2��5      $ Opacity�5       1.05       0.36$   $	 0:0:0.6D�6-   $

 True��5       0:0:0.25      W� 5"      $
 Dot3��5#      $ Opacity�5$       1.05%       0.36$   $	 0:0:0.6D�6-   $

 True��5&       0:0:0.45'      5(      5)      5*      5,      